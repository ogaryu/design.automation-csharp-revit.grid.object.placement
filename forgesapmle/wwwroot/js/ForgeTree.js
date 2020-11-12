/////////////////////////////////////////////////////////////////////
// Copyright (c) Autodesk, Inc. All rights reserved
// Written by Forge Partner Development
//
// Permission to use, copy, modify, and distribute this software in
// object code form for any purpose and without fee is hereby granted,
// provided that the above copyright notice appears in all copies and
// that both that copyright notice and the limited warranty and
// restricted rights notice below appear in all supporting
// documentation.
//
// AUTODESK PROVIDES THIS PROGRAM "AS IS" AND WITH ALL FAULTS.
// AUTODESK SPECIFICALLY DISCLAIMS ANY IMPLIED WARRANTY OF
// MERCHANTABILITY OR FITNESS FOR A PARTICULAR USE.  AUTODESK, INC.
// DOES NOT WARRANT THAT THE OPERATION OF THE PROGRAM WILL BE
// UNINTERRUPTED OR ERROR FREE.
/////////////////////////////////////////////////////////////////////

$(document).ready(function () {
  // first, check if current visitor is signed in
  jQuery.ajax({
    url: '/api/forge/oauth/token',
    success: function (res) {
      // yes, it is signed in...
      $('#sign-out').show();
      $('#refresh-hubs').show();

      // prepare sign out
      $('#sign-out').click(function () {
        $('#hidden-frame').on('load', function (event) {
          location.href = '/api/forge/oauth/signout';
        });
        $('#hidden-frame').attr('src', 'https://accounts.autodesk.com/Authentication/LogOut');
        // learn more about this signout iframe at
        // https://forge.autodesk.com/blog/log-out-forge
      })

      // and refresh button
      $('#refresh-hubs').click(function () {
        $('#user-hubs').jstree(true).refresh();
      });

      // finally:
      prepareUserHubsTree();
      showUser();
    }
  });

  $('#autodeskSigninButton').click(function () {
    jQuery.ajax({
      url: '/api/forge/oauth/url',
      success: function (url) {
        location.href = url;
      }
    });
  })
});

function prepareUserHubsTree() {
    $('#user-hubs').jstree({
        'core': {
            'themes': { "icons": true },
            'multiple': false,
            'data': {
                "url": '/api/forge/datamanagement',
                "dataType": "json",
                'cache': false,
                'data': function (node) {
                    $('#user-hubs').jstree(true).toggle_node(node);
                    return { "id": node.id };
                }
            }
        },
        'types': {
            'default': { 'icon': 'glyphicon glyphicon-question-sign' },
            '#': { 'icon': 'glyphicon glyphicon-user' },
            'hubs': { 'icon': 'https://github.com/Autodesk-Forge/bim360appstore-data.management-nodejs-transfer.storage/raw/master/www/img/a360hub.png' },
            'personalHub': { 'icon': 'https://github.com/Autodesk-Forge/bim360appstore-data.management-nodejs-transfer.storage/raw/master/www/img/a360hub.png' },
            'bim360Hubs': { 'icon': 'https://github.com/Autodesk-Forge/bim360appstore-data.management-nodejs-transfer.storage/raw/master/www/img/bim360hub.png' },
            'bim360projects': { 'icon': 'https://github.com/Autodesk-Forge/bim360appstore-data.management-nodejs-transfer.storage/raw/master/www/img/bim360project.png' },
            'a360projects': { 'icon': 'https://github.com/Autodesk-Forge/bim360appstore-data.management-nodejs-transfer.storage/raw/master/www/img/a360project.png' },
            'folders': { 'icon': 'glyphicon glyphicon-folder-open' },
            'items': { 'icon': 'glyphicon glyphicon-file' },
            'bim360documents': { 'icon': 'glyphicon glyphicon-file' },
            'versions': { 'icon': 'glyphicon glyphicon-time' },
            'unsupported': { 'icon': 'glyphicon glyphicon-ban-circle' }
        },
        "sort": function (a, b) {
            var a1 = this.get_node(a);
            var b1 = this.get_node(b);
            var parent = this.get_node(a1.parent);
            if (parent.type === 'items') { // sort by version number
                var id1 = Number.parseInt(a1.text.substring(a1.text.indexOf('v') + 1, a1.text.indexOf(':')))
                var id2 = Number.parseInt(b1.text.substring(b1.text.indexOf('v') + 1, b1.text.indexOf(':')));
                return id1 > id2 ? 1 : -1;
            }
            else if (a1.type !== b1.type) return a1.icon < b1.icon ? 1 : -1; // types are different inside folder, so sort by icon (files/folders)
            else return a1.text > b1.text ? 1 : -1; // basic name/text sort
        },
        "plugins": ["types", "state", "sort", "contextmenu"],
        "state": { "key": "autodeskHubs" }, // key restore tree state
        contextmenu: { items: autodeskCustomMenu }
    }).bind("activate_node.jstree", function (evt, data) {
        if (data != null && data.node != null && (data.node.type == 'versions' || data.node.type == 'bim360documents')) {
			
			// This sample uses context menu to hook functions => See autodeskCustomMenu()
			
            // in case the node.id contains a | then split into URN & viewableId
            //var urnViables = data.node.id.split('/')[0];
            //if (urnViables.indexOf('|') > -1) {
            //    var urn = urnViables.split('|')[0];
            //    var viewableId = urnViables.split('|')[1];
            //    launchViewer(urn, viewableId);
            //}
            //else {
            //    launchViewer(urnViables);
            //}

        }
    });
}

function prepareItemVersions() {

    jQuery.ajax({
        url: 'api/forge/datamanagement/itemversions',
        success: function (data) {
            console.log(data);
        }
    });

}

function showUser() {
  jQuery.ajax({
    url: '/api/forge/user/profile',
    success: function (profile) {
      var img = '<img src="' + profile.picture + '" height="30px">';
        $('#user-info').html(img + profile.name);
    }
  });
}

function autodeskCustomMenu(autodeskNode) {
    var items;

    switch (autodeskNode.type) {
        case "folders":
            items = {
                selectFile: {
                    label: "Select Output Folder",
                    action: function () {
                        var treeNode = $('#user-hubs').jstree(true).get_selected(true)[0];
                        selectOutputFolder(treeNode);
                    },
                    icon: 'glyphicon glyphicon-cloud-download'
                }
            };
            break;
        case "versions":
            items = {
                showViewer: {
                    label: "Show Viewer",
                    action: function () {
                        var treeNode = $('#user-hubs').jstree(true).get_selected(true)[0];
                        var urnViables = treeNode.id.split('/')[0];

                        if (urnViables.indexOf('|') > -1) {
                            var urn = urnViables.split('|')[0];
                            var viewableId = urnViables.split('|')[1];
                            launchViewer(urn, viewableId);
                        }
                        else {
                            launchViewer(urnViables);
                        }

                        selectRevitProject(treeNode);
                    },
                    icon: 'glyphicon glyphicon-eye-open'
                },
                selectFile: {
                    label: "Select Family",
                    action: function () {
                        var treeNode = $('#user-hubs').jstree(true).get_selected(true)[0];
                        selectFamily(treeNode);
                    },
                    icon: 'glyphicon glyphicon-cloud-upload'
                }
                
            };
            break;
    }

    return items;
}

function selectRevitProject(treeNode) {
    setRevitFile(treeNode);
}

function selectFamily(treeNode) {
    setFamilyFile(treeNode);
}

function selectOutputFolder(treeNode) {
    setOutputFolder(treeNode);
}