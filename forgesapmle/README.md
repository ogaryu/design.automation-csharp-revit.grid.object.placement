# designautomation.revit - ASP.NET Core

![Platforms](https://img.shields.io/badge/platform-Windows|MacOS-lightgray.svg)
![.NET](https://img.shields.io/badge/.NET%20Core-2.1-blue.svg)
[![License](http://img.shields.io/:license-MIT-blue.svg)](http://opensource.org/licenses/MIT)

[![oAuth2](https://img.shields.io/badge/oAuth2-v1-green.svg)](http://developer.autodesk.com/)
[![Data-Management](https://img.shields.io/badge/Data%20Management-v1-green.svg)](http://developer.autodesk.com/)
[![Design-Automation](https://img.shields.io/badge/Design%20Automation-v3-green.svg)](http://developer.autodesk.com/)

![Advanced](https://img.shields.io/badge/Level-Advanced-blue.svg)

# Description

To run this sample,the firt step is to prepare the app bundle package.
See **[readme](DA4R_GridObjectPlacement/)** for plugin details.

The second step is to create Appbundle and Activity through Postman collection.
See **[readme](postman/)** for the details.

## Thumbnail

![thumbnail](../thumbnail.gif)

# Setup

## Prerequisites

1. **Forge Account**: Learn how to create a Forge Account, activate subscription and create an app at [this tutorial](http://learnforge.autodesk.io/#/account/). 
2. **Visual Studio**: Either Community (Windows) or Code (Windows, MacOS).
3. **.NET Core** basic knowledge with C#
4. **ngrok**: Routing tool, [download here](https://ngrok.com/). 

## Running locally

Clone this project or download it. It's recommended to install [GitHub desktop](https://desktop.github.com/). To clone it via command line, use the following (**Terminal** on MacOSX/Linux, **Git Shell** on Windows):

    git clone https://github.com/ogaryu/design.automation-csharp-revit.grid.object.placement.git
    
**ngrok**

When a `Workitem` completes, **Design Automation** can notify our application. As the app is running locally (i.e. `localhost`), it's not reacheable from the internet. `ngrok` tool creates a temporary address that channels notifications to our `localhost` address.

After [download ngrok](https://ngrok.com/), run `ngrok http 3000 -host-header="localhost:3000"`, then copy the `http` address into the `FORGE_WEBHOOK_URL` environment variable (see next). For this sample, do not use the `https` address.

![](../media/webapp/ngrok_setup.png)

**Visual Studio** (Windows):

Right-click on the project, then go to **Debug**. Adjust the settings as shown below. 

![](../media/webapp/visual_studio_settings.png)

**Visual Sutdio Code** (Windows):

Open the `webapp` folder (only), at the bottom-right, select **Yes** and **Restore**. This restores the packages (e.g. Autodesk.Forge) and creates the launch.json file.

![](../media/webapp/visual_code_restore.png)

At the `.vscode\launch.json`, find the env vars and add your Forge Client ID, Secret and callback URL. Also define the `ASPNETCORE_URLS` variable. The end result should be as shown below:

```json
"env": {
    "ASPNETCORE_ENVIRONMENT": "Development",
    "ASPNETCORE_URLS" : "http://localhost:3000",
    "FORGE_CLIENT_ID": "your id here",
    "FORGE_CLIENT_SECRET": "your secret here",
    "FORGE_WEBHOOK_URL": "http://1234.ngrok.io",
    "FORGE_NICKNAME": "your nickname here"
},
```

**How to use this sample**

1. Upload a sample family project in BIM360 [Project Files] folder. You can find the sample in [sample file] folder.
2. Create a new folder in BIM360 [Project Files] folder to export output zip file. 
3. Run the forgesample project.
4. Open `http://localhost:3000` to start the app.
5. Login to BIM 360 and select a Revit project file stored in BIM 360 project folder, then you can see the model in the Forge Viewer.
6. Launch the Custom Extension in the Viewer.
7. Select a room and select the Revit project file in which the family instances are located.
8. Set the input parameter values for the variables and select the output folder.
9. Select a grid pattern. We have selected the step grid pattern.
10. Click the Generate button, and after a few tens of minutes, you will get a ZIP file including multiple DWG file with the layout where the addin could place the most instances per generation.


# Further Reading

Documentation:

- [Design Automation v3](https://forge.autodesk.com/en/docs/design-automation/v3/developers_guide/overview/)
- [Data Management](https://forge.autodesk.com/en/docs/data/v2/reference/http/) used to store input and output files.

Other APIs:

- [.NET Core SignalR](https://docs.microsoft.com/en-us/aspnet/core/signalr/introduction?view=aspnetcore-2.2)

### Troubleshooting

1. **error setting certificate verify locations** error: may happen on Windows, use the following: `git config --global http.sslverify "false"`

## License

This sample is licensed under the terms of the [MIT License](http://opensource.org/licenses/MIT). Please see the [LICENSE](LICENSE) file for full details.

## Written by

Ryuji Ogasawara, [Forge Partner Development](http://forge.autodesk.com)