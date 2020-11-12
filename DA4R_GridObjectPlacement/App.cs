#region Namespaces
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using DesignAutomationFramework;
using Newtonsoft.Json;

using GeneticSharp.Domain;
using GeneticSharp.Domain.Chromosomes;
using GeneticSharp.Domain.Crossovers;
using GeneticSharp.Domain.Fitnesses;
using GeneticSharp.Domain.Mutations;
using GeneticSharp.Domain.Populations;
using GeneticSharp.Domain.Selections;
using GeneticSharp.Domain.Terminations;
#endregion

namespace DA4R_GridObjectPlacement
{
    class App : IExternalDBApplication
    {
        Document m_doc;
        Application m_app;
        View m_view;
        FamilySymbol m_familySymbol;
        Room m_room;
        Level m_level;

        // Distance between objects - position X
        double m_objectDistanceX = 0;

        // Distance between objects - position Y
        double m_objectDistanceY = 0;

        // Minimum distance from objects to wall
        double m_minimumDistanceFromObjectToWall = 0;

        // Selected Placement Method
        int m_selectedPlacementMethod;
        public ExternalDBApplicationResult OnStartup(Autodesk.Revit.ApplicationServices.ControlledApplication app)
        {
            DesignAutomationBridge.DesignAutomationReadyEvent += HandleDesignAutomationReadyEvent;
            return ExternalDBApplicationResult.Succeeded;
        }

        public ExternalDBApplicationResult OnShutdown(Autodesk.Revit.ApplicationServices.ControlledApplication app)
        {
            return ExternalDBApplicationResult.Succeeded;
        }

        public void HandleDesignAutomationReadyEvent(object sender, DesignAutomationReadyEventArgs e)
        {
            e.Succeeded = true;
            GridObjectPlacement(e.DesignAutomationData);
        }

        /**
         * 
         * Genetic Algorithm with Revit API
         * 
         * This sample is using GeneticSharp Library (github.com/giacomelli/GeneticSharp)
         * The MIT License (MIT)
         * Copyright (c) 2013 Diego Giacomelli
         */
        public void GridObjectPlacement(DesignAutomationData data)
        {
            m_doc = data.RevitDoc;
            m_app = data.RevitApp;

            InputData inputParameters = JsonConvert.DeserializeObject<InputData>(File.ReadAllText("params.json"));

            // Family Symbol
            Document familyProjectDoc = m_app.OpenDocumentFile("family.rvt");

            string tempFamilyName = Path.GetFileNameWithoutExtension(inputParameters.FamilyFileName) + ".rfa";
            ModelPath tempFamilyModelPath = ModelPathUtils.ConvertUserVisiblePathToModelPath(tempFamilyName);

            FamilySymbol tempFamilySymbol = null;

            FilteredElementCollector familyInstanceCollector
              = new FilteredElementCollector(familyProjectDoc)
                .WhereElementIsNotElementType()
                .OfCategory(BuiltInCategory.OST_Furniture)
                .OfClass(typeof(FamilyInstance));

            foreach(Element familyInstanceElem in familyInstanceCollector)
            {
                FamilyInstance fi = familyInstanceElem as FamilyInstance;

                Element superComponent = fi.SuperComponent;

                if (superComponent == null)
                {
                    tempFamilySymbol = fi.Symbol;

                    Family family = tempFamilySymbol.Family;

                    Document familyDoc = familyProjectDoc.EditFamily(family);

                    Family loadedFamily = familyDoc.LoadFamily(m_doc);

                    ISet<ElementId> familySymbolIds = loadedFamily.GetFamilySymbolIds();

                    foreach(ElementId familySymbolId in familySymbolIds)
                    {
                        FamilySymbol familySymbol = m_doc.GetElement(familySymbolId) as FamilySymbol;

                        m_familySymbol = familySymbol;
                    }

                    break;
                }
            }
            
            if (!m_familySymbol.IsActive)
            {
                using (Transaction tx = new Transaction(m_doc))
                {
                    tx.Start("Transaction Activate Family Symbol");
                    m_familySymbol.Activate();
                    tx.Commit();
                }
            }

            // Room
            m_room = m_doc.GetElement(inputParameters.RoomUniqueId) as Room;

            // Level
            m_level = m_doc.GetElement(m_room.LevelId) as Level;

            // View
            ElementId viewId = m_level.FindAssociatedPlanViewId();
            if (viewId != null)
            {
                m_view = m_doc.GetElement(viewId) as View;
            }

            // Selected Placement Method
            m_selectedPlacementMethod = int.Parse(inputParameters.GridTypeId);

            // Construct Chromosomes with 3 params, m_objectDistanceX, m_objectDistanceY, m_selectedPlacementMethod
            var chromosome = new FloatingPointChromosome(
                new double[] { double.Parse(inputParameters.DistanceXMinParam), double.Parse(inputParameters.DistanceYMinParam), double.Parse(inputParameters.DistanceWallMinParam) },
                new double[] { double.Parse(inputParameters.DistanceXMaxParam), double.Parse(inputParameters.DistanceYMaxParam), double.Parse(inputParameters.DistanceWallMaxParam) },
                new int[] { 32, 32, 32 },
                new int[] { 2, 2, 2 });

            // Population Settings
            //
            // The population size needs to be 'large enough'.
            // The question of when a population is large enough is difficult to answer.
            // Generally, it depends on the project, the number of genes, and the gene value range. 
            // A good rule of thumb is to set the population size to at least 3x the number of inputs. 
            // If the results don't start to converge to an answer, you may need to increase the population size.
            //
            // by www.generativedesign.org/02-deeper-dive/02-04_genetic-algorithms/02-04-02_initialization-phase
            var population = new Population(8, 12, chromosome);

            // Fitness Function Settings
            //
            // Call CreateObjectPlacementPointList() and get count of points. 
            // This sample maximize a number of objects to place in a room.
            // 
            // A fitness function is used to evaluate how close (or far off) a given design solution is from meeting the designerfs goals.
            //
            // by www.generativedesign.org/02-deeper-dive/02-04_genetic-algorithms/02-04-03_evaluation-phase
            var fitness = new FuncFitness((c) =>
            {
                var fc = c as FloatingPointChromosome;

                var values = fc.ToFloatingPoints();

                m_objectDistanceX = values[0];
                m_objectDistanceY = values[1];
                m_minimumDistanceFromObjectToWall = values[2];

                List<XYZ> objectPlacementPointList = CreateObjectPlacementPointList();

                return objectPlacementPointList.Count;
            });

            var selection = new EliteSelection();
            var crossover = new UniformCrossover(0.5f);
            var mutation = new FlipBitMutation();

            // Termination Condition Settings
            //
            // To finish the process in half an hour, this sample sets 2 conditions.
            var termination = new OrTermination(
                   new GenerationNumberTermination(20),
                   new TimeEvolvingTermination(TimeSpan.FromMinutes(10)));

            // Construct GeneticAlgorithm
            var ga = new GeneticAlgorithm(
                population,
                fitness,
                selection,
                crossover,
                mutation);

            ga.Termination = termination;

            Console.WriteLine("Generation: objectDistanceX, objectDistanceY, minimumDistanceFromObjectToWall = objectCount");

            var latestFitness = 0.0;

            // Callback Function of Generation Result
            ga.GenerationRan += (sender, e) =>
            {
                var bestChromosome = ga.BestChromosome as FloatingPointChromosome;
                var bestFitness = bestChromosome.Fitness.Value;

                if (bestFitness != latestFitness)
                {
                    latestFitness = bestFitness;
                    var phenotype = bestChromosome.ToFloatingPoints();

                    m_objectDistanceX = phenotype[0];
                    m_objectDistanceY = phenotype[1];
                    m_minimumDistanceFromObjectToWall = phenotype[2];

                    Console.WriteLine(
                        "Generation {0,2}: objectDistanceX: {1}, objectDistanceY: {2}, minimumDistanceFromObjectToWall: {3} = objectCount: {4}",
                        ga.GenerationsNumber,
                        m_objectDistanceX,
                        m_objectDistanceY,
                        m_minimumDistanceFromObjectToWall,
                        bestFitness
                    );

                    List<XYZ> objectPlacementPointList = CreateObjectPlacementPointList();

                    using (Transaction tx = new Transaction(m_doc))
                    {
                        tx.Start("Transaction Create Family Instance");

                        m_view.SetCategoryHidden(new ElementId(BuiltInCategory.OST_Furniture), false);

                        foreach (XYZ point in objectPlacementPointList)
                        {
                            FamilyInstance fi = m_doc.Create.NewFamilyInstance(point, m_familySymbol, m_level, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);

                            m_doc.Regenerate();

                            BoundingBoxXYZ fiBB = fi.get_BoundingBox(m_view);

                            double xDiff = fiBB.Max.X - fiBB.Min.X;
                            double yDiff = fiBB.Max.Y - fiBB.Min.Y;

                            if (m_objectDistanceX / m_objectDistanceY >= 1.2 && yDiff / xDiff >= 1.2)
                            {
                                LocationPoint location = fi.Location as LocationPoint;

                                if (null != location)
                                {
                                    XYZ fiLocationPoint = location.Point;

                                    XYZ axisPoint = new XYZ(fiLocationPoint.X, fiLocationPoint.Y, fiLocationPoint.Z + 10);

                                    Line axis = Line.CreateBound(fiLocationPoint, axisPoint);

                                    location.Rotate(axis, Math.PI / 2.0);
                                }
                            }
                            else if (m_objectDistanceY / m_objectDistanceX >= 1.2 && xDiff / yDiff >= 1.2)
                            {
                                LocationPoint location = fi.Location as LocationPoint;

                                if (null != location)
                                {
                                    XYZ fiLocationPoint = location.Point;

                                    XYZ axisPoint = new XYZ(fiLocationPoint.X, fiLocationPoint.Y, fiLocationPoint.Z + 10);

                                    Line axis = Line.CreateBound(fiLocationPoint, axisPoint);

                                    location.Rotate(axis, Math.PI / 2.0);
                                }
                            }
                        }

                        DWGExportOptions dwgOptions = new DWGExportOptions();

                        ICollection<ElementId> views = new List<ElementId>();
                        views.Add(m_view.Id);

                        m_doc.Export(Directory.GetCurrentDirectory() + "\\exportedDwgs", m_level.Name + "_" + m_room.Name + "_Gen " + ga.GenerationsNumber + "_" + DateTime.Now.ToString("yyyyMMddHHmmss"), views, dwgOptions);

                        tx.RollBack();
                    }
                }
            };

            ga.Start();
        }

        public List<XYZ> CreateObjectPlacementPointList()
        {
            // Get room finish boundary curve loop
            CurveLoop roomFinishBoundary = GetRoomFinishBoundarySolid();

            // Create a new boundary curve loop with setting offset distance from wall to inner area
            CurveLoop internalOffsetRoomFinishBoundary = CurveLoop.CreateViaOffset(roomFinishBoundary, -m_minimumDistanceFromObjectToWall, XYZ.BasisZ);

            // Create a solid from roomFinishBoundary and internalOffsetRoomFinishBoundary
            IList<CurveLoop> internalRoomBoundaryCurveLoopList = new List<CurveLoop>();
            internalRoomBoundaryCurveLoopList.Add(internalOffsetRoomFinishBoundary);

            Solid internalRoomBoundarySolid = GeometryCreationUtilities.CreateExtrusionGeometry(internalRoomBoundaryCurveLoopList, XYZ.BasisZ, 1.0);

            // Get a bottom face of the solid
            Face internalRoomBoundaryFace = GetBottomPlanarFaceFromSolid(internalRoomBoundarySolid);

            // Get outlines of door boundingbox
            List<Outline> doorsOutlineList = GetDoorsOutlineList();

            // Get outlines of columns
            List<Outline> coulumnsOutlineList = GetColumnsOutlineList();

            // get outlines of walls
            List<Outline> wallsOutlineList = GetWallsOutlineList();

            // Create UV points on a room boundary face
            List<UV> uvPoints = CreateUVPointsOnRoomBoundaryFace(internalRoomBoundaryFace);

            // Get XYZ points on a room boundary face
            List<XYZ> pointsInsideRoom = new List<XYZ>();

            foreach (UV uvPoint in uvPoints)
            {
                pointsInsideRoom.Add(internalRoomBoundaryFace.Evaluate(uvPoint));
            }

            // Remove points where family instance intersects with doors and columns
            List<XYZ> objectPlacementPointList = RemoveObjectIntersectionPoints(pointsInsideRoom, doorsOutlineList, coulumnsOutlineList, wallsOutlineList);

            return objectPlacementPointList;
        }

        public bool CalculateUVRangeForRectangularGrid(double maxDist, double min, double max, double objDist, out List<double> uvList)
        {
            uvList = new List<double>();

            double actualAmount = Math.Floor(maxDist / objDist);

            double actualDistance = maxDist / actualAmount;

            double addedDist = min;

            while (addedDist <= max)
            {
                uvList.Add(addedDist);

                addedDist += actualDistance;
            }

            return true;
        }

        public bool CalculateUVRangeForSteppedGrid(double maxDist, double min, double max, double objDist, out List<double> uvList, out List<double> uvListOverlap)
        {
            uvList = new List<double>();
            uvListOverlap = new List<double>();

            double actualAmount = Math.Floor(maxDist / objDist);

            double actualDistance = maxDist / actualAmount;

            double addedDist = min;
            double addedDistOverlap = min + actualDistance;

            while (addedDist <= max)
            {
                uvList.Add(addedDist);

                if (addedDistOverlap <= max)
                {
                    uvListOverlap.Add(addedDistOverlap);
                }

                addedDist += actualDistance * 2;
                addedDistOverlap += actualDistance * 2;
            }

            return true;
        }
        public CurveArray ConvertLoopToArray(CurveLoop loop)
        {
            CurveArray a = new CurveArray();
            foreach (Curve c in loop)
            {
                a.Append(c);
            }
            return a;
        }

        public PlanarFace GetBottomPlanarFaceFromSolid(Solid solid)
        {
            PlanarFace resultFace = null;

            foreach (Face solidFace in solid.Faces)
            {
                if (solidFace is PlanarFace)
                {
                    PlanarFace planarFace = solidFace as PlanarFace;

                    if (planarFace.FaceNormal.IsAlmostEqualTo(new XYZ(0, 0, -1)))
                    {
                        resultFace = planarFace;
                    }
                }
            }

            return resultFace;
        }

        public Solid GetSolidFromBoundingBoxXYZ(BoundingBoxXYZ bbox)
        {
            XYZ pt0 = new XYZ(bbox.Min.X, bbox.Min.Y, bbox.Min.Z);
            XYZ pt1 = new XYZ(bbox.Max.X, bbox.Min.Y, bbox.Min.Z);
            XYZ pt2 = new XYZ(bbox.Max.X, bbox.Max.Y, bbox.Min.Z);
            XYZ pt3 = new XYZ(bbox.Min.X, bbox.Max.Y, bbox.Min.Z);

            //edges in BBox coords
            Line edge0 = Line.CreateBound(pt0, pt1);
            Line edge1 = Line.CreateBound(pt1, pt2);
            Line edge2 = Line.CreateBound(pt2, pt3);
            Line edge3 = Line.CreateBound(pt3, pt0);

            //create loop, still in BBox coords
            List<Curve> edges = new List<Curve>();
            edges.Add(edge0);
            edges.Add(edge1);
            edges.Add(edge2);
            edges.Add(edge3);

            Double height = bbox.Max.Z - bbox.Min.Z;

            CurveLoop baseLoop = CurveLoop.Create(edges);

            List<CurveLoop> loopList = new List<CurveLoop>();
            loopList.Add(baseLoop);

            Solid preTransformBox = GeometryCreationUtilities.CreateExtrusionGeometry(loopList, XYZ.BasisZ, height);

            return preTransformBox;
        }

        public double CalculateFamilyInstanceBBRadius()
        {
            BoundingBoxXYZ familySolidBB = null;

            using (Transaction tx = new Transaction(m_doc))
            {
                tx.Start("Transaction Temp");

                // Get a bottom face from room solid
                SpatialElementGeometryCalculator calculator = new SpatialElementGeometryCalculator(m_doc);
                SpatialElementGeometryResults results = calculator.CalculateSpatialElementGeometry(m_room);
                Solid roomSolid = results.GetGeometry();
                PlanarFace roomFace = GetBottomPlanarFaceFromSolid(roomSolid);

                // Create family instance temporarily
                FamilyInstance tempInstance = m_doc.Create.NewFamilyInstance(roomFace.Origin, m_familySymbol, m_level, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);

                // Get a boudingbox of family instance
                Options getometryOptions = new Options();
                getometryOptions.IncludeNonVisibleObjects = false;
                getometryOptions.View = m_view;

                GeometryElement geoElem = tempInstance.get_Geometry(getometryOptions);

                foreach (GeometryObject geoObject in geoElem)
                {
                    GeometryInstance geoInst = geoObject as GeometryInstance;

                    foreach (GeometryObject instanceGeoObject in geoInst.GetInstanceGeometry())
                    {
                        if (instanceGeoObject is Solid)
                        {
                            Solid solid = (Solid)instanceGeoObject;

                            familySolidBB = solid.GetBoundingBox();
                        }
                    }
                }

                tx.RollBack();
            }

            // Calculate perimeter of family instance
            XYZ familySolidBBMaxPt = familySolidBB.Max;
            XYZ familySolidBBMinPt = familySolidBB.Min;

            XYZ minPt = new XYZ(familySolidBBMinPt.X, familySolidBBMinPt.Y, 0.0);
            XYZ maxPt = new XYZ(familySolidBBMaxPt.X, familySolidBBMaxPt.Y, 0.0);
            double familySolidBBRadius = (minPt.DistanceTo(maxPt)) / 2;
            familySolidBBRadius = Math.Round(familySolidBBRadius, 4, MidpointRounding.AwayFromZero);

            return familySolidBBRadius;
        }

        public List<UV> CreateUVPointsOnRoomBoundaryFace(Face internalRoomBoundaryFace)
        {
            UV roomBBMaxPoint = internalRoomBoundaryFace.GetBoundingBox().Max;
            UV roomBBMinPoint = internalRoomBoundaryFace.GetBoundingBox().Min;

            double xDim = Math.Abs(roomBBMinPoint.U - roomBBMaxPoint.U);
            double yDim = Math.Abs(roomBBMinPoint.V - roomBBMaxPoint.V);

            List<double> uList = new List<double>();
            List<double> vList = new List<double>();

            List<double> uListOverlap = new List<double>();
            List<double> vListOverlap = new List<double>();

            List<UV> uvPoints = new List<UV>();

            // Calculate grid UV points
            if (m_selectedPlacementMethod == (int)PlacementMethod.RectangularGrid)
            {
                CalculateUVRangeForRectangularGrid(xDim, roomBBMinPoint.U, roomBBMaxPoint.U, m_objectDistanceX, out uList);
                CalculateUVRangeForRectangularGrid(yDim, roomBBMinPoint.V, roomBBMaxPoint.V, m_objectDistanceY, out vList);
            }
            else if (m_selectedPlacementMethod == (int)PlacementMethod.SteppedGrid)
            {
                CalculateUVRangeForSteppedGrid(xDim, roomBBMinPoint.U, roomBBMaxPoint.U, m_objectDistanceX, out uList, out uListOverlap);
                CalculateUVRangeForSteppedGrid(yDim, roomBBMinPoint.V, roomBBMaxPoint.V, m_objectDistanceY, out vList, out vListOverlap);
            }

            foreach (double uParam in uList)
            {
                foreach (double vParam in vList)
                {
                    uvPoints.Add(new UV(uParam, vParam));
                }
            }

            foreach (double uParam in uListOverlap)
            {
                foreach (double vParam in vListOverlap)
                {
                    uvPoints.Add(new UV(uParam, vParam));
                }
            }

            return uvPoints;
        }

        public List<XYZ> RemoveObjectIntersectionPoints(List<XYZ> pointsInsideRoom, List<Outline> doorsOutlineList, List<Outline> coulumnsOutlineList, List<Outline> wallsOutlineList)
        {
            List<XYZ> objectPlacementPointList = new List<XYZ>();

            using (Transaction tx = new Transaction(m_doc))
            {
                tx.Start("Transaction Temp Create Family Instance");

                foreach (XYZ centerPoint in pointsInsideRoom)
                {
                    FamilyInstance fi = m_doc.Create.NewFamilyInstance(centerPoint, m_familySymbol, m_level, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);

                    m_doc.Regenerate();

                    BoundingBoxXYZ fiBB = fi.get_BoundingBox(m_view);

                    double xDiff = fiBB.Max.X - fiBB.Min.X;
                    double yDiff = fiBB.Max.Y - fiBB.Min.Y;

                    if (m_objectDistanceX / m_objectDistanceY >= 1.2 && yDiff / xDiff >= 1.2)
                    {
                        LocationPoint location = fi.Location as LocationPoint;

                        if (null != location)
                        {
                            XYZ fiLocationPoint = location.Point;

                            XYZ axisPoint = new XYZ(fiLocationPoint.X, fiLocationPoint.Y, fiLocationPoint.Z + 10);

                            Line axis = Line.CreateBound(fiLocationPoint, axisPoint);

                            location.Rotate(axis, Math.PI / 2.0);
                        }
                    }
                    else if (m_objectDistanceY / m_objectDistanceX >= 1.2 && xDiff / yDiff >= 1.2)
                    {
                        LocationPoint location = fi.Location as LocationPoint;

                        if (null != location)
                        {
                            XYZ fiLocationPoint = location.Point;

                            XYZ axisPoint = new XYZ(fiLocationPoint.X, fiLocationPoint.Y, fiLocationPoint.Z + 10);

                            Line axis = Line.CreateBound(fiLocationPoint, axisPoint);

                            location.Rotate(axis, Math.PI / 2.0);
                        }
                    }

                    m_doc.Regenerate();

                    BoundingBoxXYZ familyInstanceBB = fi.get_BoundingBox(m_view);

                    Outline familyInstanceOutline = new Outline(familyInstanceBB.Min, familyInstanceBB.Max);

                    List<Outline> obstaclesOutlineList = doorsOutlineList.Concat(coulumnsOutlineList).ToList().Concat(wallsOutlineList).ToList();

                    bool obstaclesIsIntersected = false;

                    // Intersetion check with doors, columns, walls and family instance
                    foreach (Outline obstacleOutline in obstaclesOutlineList)
                    {
                        bool intersectionResult = familyInstanceOutline.Intersects(obstacleOutline, 1);

                        if (intersectionResult)
                        {
                            obstaclesIsIntersected = true;
                        }
                    }

                    if (!obstaclesIsIntersected)
                    {
                        objectPlacementPointList.Add(centerPoint);
                    }
                }

                tx.RollBack();
            }

            return objectPlacementPointList;
        }

        public CurveLoop GetRoomFinishBoundarySolid()
        {
            List<CurveLoop> boundarySegmentCurveLoopList = new List<CurveLoop>();

            SpatialElementBoundaryOptions options = new SpatialElementBoundaryOptions();
            options.SpatialElementBoundaryLocation = SpatialElementBoundaryLocation.Finish;

            IList<IList<BoundarySegment>> roomBoundarySegmentsListList = m_room.GetBoundarySegments(options);

            foreach (IList<BoundarySegment> boundarySegmentsList in roomBoundarySegmentsListList)
            {
                IList<Curve> boundarySegmentCurveList = new List<Curve>();

                foreach (BoundarySegment boundarySegment in boundarySegmentsList)
                {
                    boundarySegmentCurveList.Add(boundarySegment.GetCurve());
                }

                CurveLoop boundarySegmentCurveLoop = CurveLoop.Create(boundarySegmentCurveList);

                boundarySegmentCurveLoopList.Add(boundarySegmentCurveLoop);
            }

            boundarySegmentCurveLoopList.Sort((a, b) => (int)a.GetExactLength() - (int)b.GetExactLength());
            CurveLoop roomFinishBoundary = boundarySegmentCurveLoopList[boundarySegmentCurveLoopList.Count - 1];

            return roomFinishBoundary;
        }

        public List<Outline> GetDoorsOutlineList()
        {
            List<Outline> doorsOutlineList = new List<Outline>();

            BoundingBoxXYZ bb = m_room.get_BoundingBox(null);

            Outline outline = new Outline(bb.Min, bb.Max);

            BoundingBoxIntersectsFilter roomIntersectionFilter = new BoundingBoxIntersectsFilter(outline);

            FilteredElementCollector doorsCollector
              = new FilteredElementCollector(m_doc)
                .WhereElementIsNotElementType()
                .OfCategory(BuiltInCategory.OST_Doors)
                .OfClass(typeof(FamilyInstance))
                .WherePasses(roomIntersectionFilter);

            foreach (Element doorElem in doorsCollector)
            {
                FamilyInstance doorInstance = doorElem as FamilyInstance;

                if (doorElem.LevelId.IntegerValue == m_level.Id.IntegerValue && doorInstance.Room.Id.IntegerValue == m_room.Id.IntegerValue)
                {
                    BoundingBoxXYZ doorBB = doorInstance.get_BoundingBox(m_view);

                    Outline doorOutline = new Outline(doorBB.Min, doorBB.Max);

                    doorsOutlineList.Add(doorOutline);
                }
            }

            return doorsOutlineList;
        }

        public List<Outline> GetColumnsOutlineList()
        {
            List<Outline> coulumnsOutlineList = new List<Outline>();

            BoundingBoxXYZ bb = m_room.get_BoundingBox(null);

            Outline outline = new Outline(bb.Min, bb.Max);

            BoundingBoxIntersectsFilter roomIntersectionFilter = new BoundingBoxIntersectsFilter(outline);

            ElementCategoryFilter columnsCategoryfilter = new ElementCategoryFilter(BuiltInCategory.OST_Columns);
            ElementCategoryFilter structuralColumnsCategoryfilter = new ElementCategoryFilter(BuiltInCategory.OST_StructuralColumns);

            LogicalOrFilter columnInstancesFilter = new LogicalOrFilter(columnsCategoryfilter, structuralColumnsCategoryfilter);

            FilteredElementCollector columnsCollector
              = new FilteredElementCollector(m_doc)
                .WhereElementIsNotElementType()
                .OfClass(typeof(FamilyInstance))
                .WherePasses(columnInstancesFilter)
                .WherePasses(roomIntersectionFilter);

            foreach (Element columnElem in columnsCollector)
            {
                FamilyInstance fi = columnElem as FamilyInstance;

                BoundingBoxXYZ columnBB = columnElem.get_BoundingBox(m_view);

                Outline columnOutline = new Outline(columnBB.Min, columnBB.Max);

                coulumnsOutlineList.Add(columnOutline);
            }

            return coulumnsOutlineList;
        }

        public List<Outline> GetWallsOutlineList()
        {
            List<Outline> wallsOutlineList = new List<Outline>();

            BoundingBoxXYZ bb = m_room.get_BoundingBox(null);

            Outline outline = new Outline(bb.Min, bb.Max);

            BoundingBoxIntersectsFilter roomIntersectionFilter = new BoundingBoxIntersectsFilter(outline);

            FilteredElementCollector wallsCollector
              = new FilteredElementCollector(m_doc)
                .WhereElementIsNotElementType()
                .OfCategory(BuiltInCategory.OST_Walls)
                .OfClass(typeof(Wall))
                .WherePasses(roomIntersectionFilter);

            foreach (Element wallElem in wallsCollector)
            {
                Wall wallInstance = wallElem as Wall;

                if (wallInstance.LevelId.IntegerValue == m_level.Id.IntegerValue)
                {
                    BoundingBoxXYZ wallBB = wallInstance.get_BoundingBox(m_view);

                    Outline wallOutline = new Outline(wallBB.Min, wallBB.Max);

                    wallsOutlineList.Add(wallOutline);
                }
            }

            return wallsOutlineList;
        }
    }

    public class InputData
    {
        public string RoomUniqueId { get; set; }
        public string GridTypeId { get; set; }
        public string FamilyFileName { get; set; }
        public string OutputZipFileName { get; set; }
        public string DistanceXMinParam { get; set; }
        public string DistanceXMaxParam { get; set; }
        public string DistanceYMinParam { get; set; }
        public string DistanceYMaxParam { get; set; }
        public string DistanceWallMinParam { get; set; }
        public string DistanceWallMaxParam { get; set; }

    }
    enum PlacementMethod
    {
        RectangularGrid = 0,
        SteppedGrid = 1
    }
}
