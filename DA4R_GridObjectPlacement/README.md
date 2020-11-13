# designautomation.revit - Revit Appbundle sample

![Platforms](https://img.shields.io/badge/Plugins-Windows-lightgray.svg)
![.NET](https://img.shields.io/badge/.NET%20Framework-4.8-blue.svg)
[![Revit](https://img.shields.io/badge/Revit-2021-lightblue.svg)](http://developer.autodesk.com/)

![Advanced](https://img.shields.io/badge/Level-Advanced-blue.svg)

# Description

Revit 2021 introduced the generative design functionality which is available to all AEC Collections subscribers.  
Please see [Have You Tried - Generative Design](https://help.autodesk.com/view/RVT/2021/ENU/?guid=GUID-A2EC3302-CB0E-4648-A3A5-6EE0119119CD)

The Generative Design tool allows you to easily enter your design criteria.
When the iterations are complete, you can review the potential solutions.  
Please see [Generative Design](https://help.autodesk.com/view/RVT/2021/ENU/?guid=GUID-492527AD-AAB9-4BAA-82AE-9B95B6C3E5FE)

The tools provide 6 Dynamo samples including grid object placement sample.
This plug-in is converted from Grid Object Placement sample study to Revit DB application.  
Please see [Grid Object Placement](https://help.autodesk.com/view/RVT/2021/ENU/?guid=GUID-DADBD42E-84D8-4C41-B651-111121E13E8C)

Currently, “Generative Design tools" is not exposed in Revit API, this sample is using the open source library named [GeneticSharp](https://github.com/giacomelli/GeneticSharp).

# Setup

## Prerequisites

1. **Visual Studio** 2019
2. **Revit** 2021 required to compile changes into the plugin

## References

This Revit plugin requires **RevitAPI** and **DesignAutomationBridge** references. The `Reference Path` option should point to the folder.

![](../media/revit/reference_path.png)

# Further Reading

- [My First Revit Plugin](https://knowledge.autodesk.com/support/revit-products/learn-explore/caas/simplecontent/content/my-first-revit-plug-overview.html)
- [Revit Developer Center](https://www.autodesk.com/developer-network/platform-technologies/revit)

## License

This sample is licensed under the terms of the [MIT License](http://opensource.org/licenses/MIT). Please see the [LICENSE](LICENSE) file for full details.

## Written by

Naveen Kumar, [Forge Partner Development](http://forge.autodesk.com)