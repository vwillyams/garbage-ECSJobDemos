# Introduction

(Early) [documentation](Documentation/index.md)

Talk about C# job system & Entity Component System at Unite Austin
https://youtu.be/tGmnZdY5Y-E

## Installation guide for blank ECS project

> Note: If you want to have multiple versions of Unity on one machine then you need to follow [these instructions](https://docs.unity3d.com/462/Documentation/Manual/InstallingMultipleVersionsofUnity.html). The manual page is a bit old, in terms of which versions of Unity it describes, but the instructions are otherwise correct.

* Make sure you have installed the required beta version of Unity (See the Unity 2018.1 beta download link in [Unity build section](#unity-build)).
* Open the 2018.1.b version of Unity on your computer.
* Create a new Unity project and name it whatever you like. 

> Note: In Unity 2018.1 the new Project window is a little different because it offers you more than just 2D and 3D options.

* Once the project is created then navigate in the Editor menu to: __Edit__ > __Project Settings__ > __Player__ > __Other Settings__ Then set __Scripting Runtime Version__ to: __4.x equivalent__. 
* Go to your project location in your computer's file manager.
* Open the file _<project-name>/Packages/manifest.json_ in any text editor.
* Copy and paste the following package manifest into it, replacing the default contents:

```{
    "dependencies":{
        "com.unity.jobs":"0.0.3",
        "com.unity.entities":"0.0.7",
        "com.unity.collections":"0.0.4",
        "com.unity.burst": "0.1.31",
        "com.unity.mathematics":"0.0.7"
    },
    "testables": [
        "com.unity.collections",
        "com.unity.entities",
        "com.unity.jobs"
    ],
    "registry": "https://staging-packages.unity.com"
}
```

* Save the file and return to Unity and it should start loading the packages for you automatically.

## Unity build 

> Note: This is compatible with the "stable" branch of ECSJobDemos. The ECSJobDemo is no longer required to create a new ECS project as the required packages are all published now.

https://beta.unity3d.com/download/d72c02f07cbd/download.html
unity source code revision: d72c02f07cbd branch: 2018.1/scripting/jobsystem/playground

## Active development against master

Unity source code branch: 2018.1/scripting/jobsystem/playground
NOTE: (Navmesh & Raycast job API's is included in this branch)

## ECSJobDemos

Project folder for basic ECS dev & tests
* Entity Component System implementation
* Boid demo using ECS
* InstanceRenderer using Entities
* Culling demo
* NativeList, Nativehashmap and tests

## AI navigation & batched raycasts

Sandbox project for pathfinding: [LowLevelQueryAPI](UnstablePrototypes/LowLevelQueryAPI).
<!--stackedit_data:
eyJoaXN0b3J5IjpbMjQ5NjkxMjUzLDE4MjYzOTE4MzcsMjQ5Nj
kxMjUzLDE4MjYzOTE4MzddfQ==
-->
