# designautomation.revit.grid.object.placement

## Description

This sample is part of the Autodesk Univeresity 2020 Classes.

- SD473692: Design Automation for Revit: Basics and beyond
- SD473594: Design Automation for Revit: 基礎から応用へ

This sample shows how to use Genetic Algorithm to generate multiple DWG files and upload them to BIM 360 as a single zipped file. A stepped or rectangular grid pattern will be created using parameteric constraints as inputs for minimum and maximum distance between furniture families.

In an iterative process each “generation” will export and save its best fit layout DWG that has created most number of furniture instances using the given constraints.  A full end to end working sample that integrates with BIM 360 will be shared for you to extend upon.

It includes 2 projects and postman collection:

- .NET Framework plugins for **[Revit](DA4R_GridObjectPlacement/)** . See each readme for plugin details.
- .NET Core web interface to invoke Design Automation v3 and get results. See [readme](forgesample/) for more information.
- Postman Collection to register appbundle and activity on Design Automation.