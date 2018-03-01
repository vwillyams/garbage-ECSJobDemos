# RotationExample.unity

![](https://media.giphy.com/media/3o7WIPjJUcuIEze5Ww/giphy.gif)

## Basic description

1. Cubes are spawned randomly in a circle.
2. A sphere is moved along the same circle.
3. When the sphere intersects a cube, the cube rotates at a fixed rate about the y-axis.
4. When the sphere does not intersect a cube, the cube's rotation decays at a fixed rate.

## What this sample demonstrates

1. Spawning pure ECS entities/components (not GameObjects)
2. Updating positions
3. Initializing positions from GameObject transform
3. Updating rotations
4. Rendering instanced models based on a generated matrix
5. Simple example of updating components' data based on moving sphere

## Spawn cubes in a circle

![](https://i.imgur.com/xGoyVjL.png)

Create Empty object in the scene and name it RotatingCubeSpawner.

![](https://i.imgur.com/GlQ7sMB.png)

Add components to RotatingCubeSpawner:
1. Scripts/[```UnityEngine.ECS.Spawners/Spawn Random Circle component```](https://hackmd.io/CYdghmCsIJwwtAZgGwGMTwCyQBwLDpAAxZgCMkZAZmFUZgKYNA==)
2. Scripts/[```UnityEngine.ECS.Transform/Transform Position component```](https://hackmd.io/GYJgrAzAhgxg7ADgLQBMpjEgLAiEkJShIAMARgJwBsApgIwXQl1lA===)
3. Scripts/[```UnityEngine.ECS.Transform/Copy Initial Transform Position From GameObject component```](https://hackmd.io/EwTgbGIOwBwIYFoBmckEYEBY5pgmAJnIgEZoEEDGwcApvWgKxA==)

Set the properties of ```Spawn Random Circle component```
1. Prefab: [Assets/SampleAssets/TestRotatingCube.prefab](https://hackmd.io/BwFgZsAMCmCsYFoAmBmF0EgOwoGwIEMAjAYwEYEjZgtpcVhcyCsg) 
This is a prefab container which contains the components for the each Entity that will be spawned. 
2. Radius: 25 
Spawn entities 25m from the center of the circle.
3. Count: 100
Spawn 100 entities.


The ```Transform Position component``` specifies that the entity that is created from the RotatingCubeSpawner GameObject has a position in the ECS. That position is used as the center of the circle for spawning. (Required)

The ```Copy Initial Transform Position From GameObject component``` specifies that _only_ the initial value for ```Transform Position component``` in ECS will be copied from the GameObject's transform. 

## Move sphere about same circle and reset rotations when intersecting cubes

![](https://i.imgur.com/GyBUpSo.png)

Create Empty object in the scene and name it TestResetRotationSphere.

![](https://i.imgur.com/7WmSLyN.png)

Add components to TestResetRotationSphere:
1. Scripts/[```UnityEngine.ECS.Transform/Transform Position component```](https://hackmd.io/GYJgrAzAhgxg7ADgLQBMpjEgLAiEkJShIAMARgJwBsApgIwXQl1lA===)
2. Scripts/[```UnityEngine.ECS.Transform/Copy Initial Transform Position From GameObject component```](https://hackmd.io/EwTgbGIOwBwIYFoBmckEYEBY5pgmAJnIgEZoEEDGwcApvWgKxA==)
3. Scripts/[```UnityEngine.ECS.Transform/Transform Matrix component```](https://hackmd.io/MYZgRgbAnDCGC0ATAZgVgQFlARnlCyA7PLAKYAMYYsiUqEY2QA==)
4. Scripts/[```UnityEngine.ECS.Rendering/Instance Renderer component```](https://hackmd.io/EYQwDAjAnGDGAsBaAZgDhKx9gTIk8sArIgGxgDsEwxAzKQKZghA=)
5. Scripts/[```UnityEngine.ECS.SimpleMovement/Move Speed component```](https://hackmd.io/GYZghgDAnA7AjAVgLQwEwDY5ICxgEbKwRhJQDGeEAHJuCHiEA===)
6. Scripts/[```UnityEngine.ECS.SimpleMovement/Move Along Circle component```](https://hackmd.io/JwRgxgZgzARgJgBgLQDYoCYAcSAsB2ApTCBbCYPYFAVnWoFMcxqg)
7. Scripts/[```UnityEngine.ECS.SimpleRotation/Rotation Speed Reset Sphere component```](https://hackmd.io/KYBgrALAbARgHHAtMGMBMiIGY0bmgEwxAjgHYQYBjARgDMQ4og==)

Like the RotatingCubeSpawner, The ```Transform Position component``` specifies that the entity that is created from the TestResetRotationSphere GameObject has a position in the ECS and the ```Copy Initial Transform Position From GameObject component``` specifies that _only_ the initial value for ```Transform Position component``` in ECS will be copied from the GameObject's transform. 

The ```Transform Matrix component``` specifies that a 4x4 matrix should be stored. That matrix is updated automatically based on changes to the ```Transform Position component```.

Set the properties of the ```Instance Renderer component```
1. Mesh: Sphere
2. Material: InstanceMat
Assign a Material that has GPU Instancing enabled.

This component specfies that this Mesh/Material combination should be rendered with the corresponding ```Transform Matrix``` (Required)

Set the properties of the ```Move Speed component```
1. Speed: 1

This component requests that if another component is moving the ```Transform Position component``` it should respect this value and move the position at the constant speed specified.

Set the properties of the ```Move Along Circle component```
1. Center: 0,0,0
2. Radius: 25
The center and radius correspond to the circle of entities that is being spawned by RotatingCubeSpawner.

This component will update the corresponding ```Transform Position component``` at the rate specified by ```Move Speed component``` in radians per second.

Set the properties of the ```Rotation Speed Reset Sphere component```
1. Speed: 4 (radians per second)
2. Radius: 2 (meters)

This component specifies that if any other ```Transform Position component``` is within the sphere defined by the ```Transform Position component``` on this entity and the radius, the ```Transform Rotation component``` on that entity should be set to speed, if it exists.


















