function ForgeViewerDA4RExtension(viewer, options) {
    Autodesk.Viewing.Extension.call(this, viewer, options);
}

ForgeViewerDA4RExtension.prototype = Object.create(Autodesk.Viewing.Extension.prototype);
ForgeViewerDA4RExtension.prototype.constructor = ForgeViewerDA4RExtension;

ForgeViewerDA4RExtension.prototype.load = function () {
    console.log('ForgeViewerDA4RExtension has been loaded');

    startConnection();
    return true;
}

ForgeViewerDA4RExtension.prototype.unload = function () {
    // Clean our UI elements if we added any
    if (this._group) {
        this._group.removeControl(this._button);
        if (this._group.getNumberOfControls() === 0) {
            this.viewer.toolbar.removeControl(this._group);
        }
    }
    console.log('ForgeViewerDA4RExtension has been unloaded');
    return true;
}

ForgeViewerDA4RExtension.prototype.onToolbarCreated = function () {
    // Create a new toolbar group if it doesn't exist
    this._group = this.viewer.toolbar.getControl('forgeViewerDA4RExtensionToolbar');
    if (!this._group) {
        this._group = new Autodesk.Viewing.UI.ControlGroup('forgeViewerDA4RExtensionToolbar');
        this.viewer.toolbar.addControl(this._group);
    }

    // Add a new button to the toolbar group
    this._button = new Autodesk.Viewing.UI.Button('forgeViewerDA4RExtensionButton');
    this._button.onClick = (ev) => {

        if (!this._panel) {

            var template = document.querySelector('#da4r-content');

            var clone = document.importNode(template.content, true);

            this._panel = new SimplePanel(this.viewer, this.viewer.container, 'grid-object-placement-tool', 'Grid Object Placement', clone, 10, 10);

        }

        // Show/hide docking panel
        this._panel.setVisible(!this._panel.isVisible());

        $('#forge-viewer').removeAttr('data-input-params');
        $('#family-file').removeAttr('data-input-params');
        $('#output-folder').removeAttr('data-input-params');

    };
    this._button.setToolTip('Forge Viewer DA4R Extension');
    this._button.addClass('forgeViewerDA4RExtensionIcon');
    this._group.addControl(this._button);
}

Autodesk.Viewing.theExtensionManager.registerExtension('ForgeViewerDA4RExtension', ForgeViewerDA4RExtension);

SimplePanel = function (viewer, parentContainer, id, title, content, x, y) {
    this.content = content;
    this.viewer = viewer;
    Autodesk.Viewing.UI.DockingPanel.call(this, parentContainer, id, title);

    // Auto-fit to the content and don't allow resize.  Position at the coordinates given.
    //
    this.container.style.height = "550px";
    this.container.style.width = "300px";
    this.container.style.minHeight = "550px";
    this.container.style.minWidth = "300px";
    this.container.style.resize = "none";
    this.container.style.left = x + "px";
    this.container.style.top = y + "px";
};

SimplePanel.prototype = Object.create(Autodesk.Viewing.UI.DockingPanel.prototype);
SimplePanel.prototype.constructor = SimplePanel;
var $generateButton = {};

SimplePanel.prototype.initialize = function () {
    // Override DockingPanel initialize() to:
    // - create a standard title bar
    // - click anywhere on the panel to move
    // - create a close element at the bottom right
    //
    this.title = this.createTitleBar(this.titleLabel || this.container.id);
    this.container.appendChild(this.title);
    this.container.appendChild(this.content);
    var roomNode = {};

    $(document).on('click', '#grid-object-placement-tool #dropdown-room .dropdown-menu .dropdown-item', function () {

        var visibleItem = $('#dropdown-room button.dropdown-toggle');
        visibleItem.text($(this).text());
        visibleItem.attr('data-selected-uniqueid', $(this).attr('data-room-uniqueid'));

        var dbId = $(this).attr('data-room-dbId');

        viewer.fitToView([dbId]);
        viewer.hide([dbId]);
        
        validateBeforeGenerate();
    }); 

    $(document).on('click', '#grid-object-placement-tool #dropdown-grid .dropdown-menu .dropdown-item', function () {
        var visibleItem = $('#dropdown-grid button.dropdown-toggle');
        visibleItem.text($(this).text());
        visibleItem.attr('data-selected-grid', $(this).attr('data-grid-id'));
    }); 

    $(document).on('change', '#grid-object-placement-tool #family-file', function () {
        validateBeforeGenerate();
    }); 

    $(document).on('change', '#grid-object-placement-tool #output-folder', function () {
        validateBeforeGenerate();
    }); 

    $(document).on('click', '#grid-object-placement-tool button.btn-generate-design', function () {

        var revitUrlStr = $('#forge-viewer').data('input-params');
        var familyUrlStr = $('#family-file').data('input-params');
        var folderUrlStr = $('#output-folder').data('input-params');

        var revitFileId = $('#user-hubs').find('li[id^="' + revitUrlStr + '"]').parent().parent().attr('id');
        var revitFileName = $('#user-hubs').find('a[id="' + revitFileId + '_anchor"]').text();

        var familyFileId = $('#user-hubs').find('li[id^="' + familyUrlStr + '"]').parent().parent().attr('id');
        var familyFileName = $('#user-hubs').find('a[id="' + familyFileId + '_anchor"]').text();

        var folderId = $('#user-hubs').find('li[id^="' + folderUrlStr + '"]').attr('id');
        var folderName = $('#user-hubs').find('a[id="' + folderId + '_anchor"]').text();

        var roomUniquIdStr = $("#grid-object-placement-tool").find('#dropdown-room button.dropdown-toggle').attr('data-selected-uniqueid');

        var distanceXMin = $("#grid-object-placement-tool").find('#distance-x-min').val();
        var distanceXMax = $("#grid-object-placement-tool").find('#distance-x-max').val();

        var distanceYMin = $("#grid-object-placement-tool").find('#distance-y-min').val();
        var distanceYMax = $("#grid-object-placement-tool").find('#distance-y-max').val();

        var distanceWallMin = $("#grid-object-placement-tool").find('#distance-wall-min').val();
        var distanceWallMax = $("#grid-object-placement-tool").find('#distance-wall-max').val();

        var gridTypeIdStr = $("#grid-object-placement-tool").find('#dropdown-grid button.dropdown-toggle').attr('data-selected-grid');

        var validationResult = validateBeforeGenerate();

        if (validationResult == true) {

            $generateButton = $('#grid-object-placement-tool button.btn-generate-design').button('loading');

            startConnection(function () {
                var formData = new FormData();
                formData.append('data', JSON.stringify({
                    inputRvtFileUrl: revitUrlStr,
                    inputRvtFileName: revitFileName,
                    inputFamilyFileUrl: familyUrlStr,
                    inputFamilyFileName: familyFileName,
                    outputFolderUrl: folderUrlStr,
                    roomUniquId: roomUniquIdStr,
                    gridTypeId: gridTypeIdStr,
                    distanceXMinParam: distanceXMin,
                    distanceXMaxParam: distanceXMax,
                    distanceYMinParam: distanceYMin,
                    distanceYMaxParam: distanceYMax,
                    distanceWallMinParam: distanceWallMin,
                    distanceWallMaxParam: distanceWallMax,
                    activityName: "GridObjectPlacementActivity+test",
                    browerConnectionId: connectionId
                }));
                console.log('Request a workitem...');
                $.ajax({
                    url: 'api/forge/designautomation/grid_object_placement',
                    data: formData,
                    processData: false,
                    contentType: false,
                    type: 'POST',
                    success: function (res) {
                        console.log('Workitem started: ' + res.workItemId);
                    }
                });
            });
        }
    }); 

    getAllLeafComponents(this.viewer, function (dbIds) {

        $.each(dbIds, function (num, dbid) {

            viewer.getProperties(dbid,

                function (item) {

                    $.each(item.properties, function (num, prop) {

                        if (prop.displayName == 'CategoryId' && prop.displayValue == '-2000160') {

                            console.log(dbid);

                            viewer.getObjectTree(function (objectTree) {
                                var tree = objectTree;

                                viewer.getProperties(dbid, function (properties) {
                                    $("#grid-object-placement-tool").find("#dropdown-room ul.dropdown-menu").append("<li><a href='#' class='dropdown-item' data-room-uniqueId='" + properties.externalId + "' data-room-dbId='" + dbid + "'>" + tree.getNodeName(dbid) + "</a></li>");
                                });
                            });

                            roomNode[dbid] = dbid;

                        }
                    })

                    
                },
                function (error) {
                    console.log(error);
                });
        });

        console.log('Found ' + dbIds.length + ' leaf nodes');
    });

    this.initializeMoveHandlers(this.title);
    this.closer = this.getDocument().querySelector("#grid-object-placement-tool .docking-panel-close");
    this.initializeCloseHandler(this.closer);
    this.container.appendChild(this.closer);
};

var connection;
var connectionId;

function startConnection(onReady) {
    if (connection && connection.connectionState) { if (onReady) onReady(); return; }
    connection = new signalR.HubConnectionBuilder().withUrl("/api/signalr/designautomation").build();
    connection.start()
        .then(function () {
            connection.invoke('getConnectionId')
                .then(function (id) {
                    connectionId = id;
                    console.log(connectionId);
                    if (onReady) onReady();
                });
        });

    connection.on("onCompleteGridObjectPlacement", function (data) {
        var outputData = JSON.parse(data);

        console.log(outputData.reportLog);

        $generateButton.button('reset');
        
    });
}

function getAllLeafComponents(viewer, callback) {
    var cbCount = 0; // count pending callbacks
    var components = []; // store the results
    var tree; // the instance tree

    function getLeafComponentsRec(parent) {
        cbCount++;
        if (tree.getChildCount(parent) != 0) {
            tree.enumNodeChildren(parent, function (children) {
                getLeafComponentsRec(children);
            }, false);
        } else {
            components.push(parent);
        }
        if (--cbCount == 0) callback(components);
    }
    viewer.getObjectTree(function (objectTree) {
        tree = objectTree;
        var allLeafComponents = getLeafComponentsRec(tree.getRootId());
    });
}

function setRevitFile(treeNode) {
    $('#forge-viewer').data('input-params', treeNode.id);
}

function setFamilyFile(treeNode) {
    $('#family-file').data('input-params', treeNode.id);
    $('#family-file').val(treeNode.text).change();
}

function setOutputFolder(treeNode) {
    $('#output-folder').data('input-params', treeNode.id);
    $('#output-folder').val(treeNode.text).change();
}

function validateBeforeGenerate() {

    var roomUniquIdStr = $("#grid-object-placement-tool").find('button.dropdown-toggle').attr('data-selected-uniqueid');
    var familyUrlStr = $('#family-file').data('input-params');
    var folderUrlStr = $('#output-folder').data('input-params');

    var validationResult = false;

    if (roomUniquIdStr == null) {
        $("#btn-select-room").addClass('alert-message');
        $('#grid-object-placement-tool button.btn-generate-design').addClass('disabled');
    }
    else if (familyUrlStr == null) {
        $("#family-file").addClass('alert-message');
        $('#grid-object-placement-tool button.btn-generate-design').addClass('disabled');
    }
    else if (folderUrlStr == null) {
        $("#family-file").addClass('output-folder');
        $('#grid-object-placement-tool button.btn-generate-design').addClass('disabled');
    }
    else {
        $("#btn-select-room").removeClass('alert-message');
        $("#family-file").removeClass('alert-message');
        $("#output-folder").removeClass('alert-message');
        $('#grid-object-placement-tool button.btn-generate-design').removeClass('disabled');

        validationResult = true;
    }

    return validationResult;
}