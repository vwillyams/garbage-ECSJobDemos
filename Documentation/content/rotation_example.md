# RotationExample.unity

![](https://media.giphy.com/media/3o7WIPjJUcuIEze5Ww/giphy.gif)

## Basic description

In this example you will find:

1. Cubes are spawned randomly in a circle.
2. A sphere moves around that circle.
3. When the sphere intersects a cube, the cube rotates at a fixed rate about the y-axis.
4. When the sphere stops intersecting a cube, the cube's rotation decays at a fixed rate.

## What this example demonstrates

This examples shows you:

1. Spawning pure ECS Entities/components (not GameObjects)
2. Updating positions
3. Initializing positions from GameObject transform
3. Updating rotations
4. Rendering instanced models based on a generated matrix
5. Simple example of updating ComponentData based on a moving sphere

## Spawn cubes in a circle

![](https://i.imgur.com/xGoyVjL.png)

Create Empty GameObject in the Scene and name it "RotatingCubeSpawner".

![](https://i.imgur.com/GlQ7sMB.png)

Add components to RotatingCubeSpawner:
1. Scripts/[__UnityEngine.ECS.Spawners/SpawnRandomCircleComponent__](https://hackmd.io/CYdghmCsIJwwtAZgGwGMTwCyQBwLDpAAxZgCMkZAZmFUZgKYNA==)
2. Scripts/[__UnityEngine.ECS.Transform/TransformPositionComponent__](https://hackmd.io/GYJgrAzAhgxg7ADgLQBMpjEgLAiEkJShIAMARgJwBsApgIwXQl1lA===)
3. Scripts/[__UnityEngine.ECS.Transform/CopyInitialTransformPositionFromGameObjectComponent__](https://hackmd.io/EwTgbGIOwBwIYFoBmckEYEBY5pgmAJnIgEZoEEDGwcApvWgKxA==)

Set the properties of __SpawnRandomCircleComponent__
1. Prefab: [Assets/SampleAssets/TestRotatingCube.prefab](https://hackmd.io/BwFgZsAMCmCsYFoAmBmF0EgOwoGwIEMAjAYwEYEjZgtpcVhcyCsg) 
This is a prefab container which contains the components for the each Entity that will be spawned. 
2. Radius: 25 
Spawn entities 25m from the center of the circle.
3. Count: 100
Spawn 100 entities.


The __TransformPositionComponent__ specifies that the entity that is created from the RotatingCubeSpawner GameObject has a position in the ECS. That position is used as the center of the circle for spawning. (Required)

The __CopyInitialTransformPositionFromGameObjectComponent__ specifies that _only_ the initial value for __TransformPositionComponent__ in ECS will be copied from the GameObject's transform. 

## Move sphere about same circle and reset rotations when intersecting cubes

![](https://i.imgur.com/GyBUpSo.png)

Create Empty object in the scene and name it TestResetRotationSphere.

![](https://i.imgur.com/7WmSLyN.png)

Add components to TestResetRotationSphere:
1. Scripts/[__UnityEngine.ECS.Transform/TransformPositionComponent__](https://hackmd.io/GYJgrAzAhgxg7ADgLQBMpjEgLAiEkJShIAMARgJwBsApgIwXQl1lA===)
2. Scripts/[__UnityEngine.ECS.Transform/CopyInitialTransformPositionFromGameObjectComponent__](https://hackmd.io/EwTgbGIOwBwIYFoBmckEYEBY5pgmAJnIgEZoEEDGwcApvWgKxA==)
3. Scripts/[__UnityEngine.ECS.Transform/TransformMatrixComponent__](https://hackmd.io/MYZgRgbAnDCGC0ATAZgVgQFlARnlCyA7PLAKYAMYYsiUqEY2QA==)
4. Scripts/[__UnityEngine.ECS.Rendering/InstanceRendererComponent__](https://hackmd.io/EYQwDAjAnGDGAsBaAZgDhKx9gTIk8sArIgGxgDsEwxAzKQKZghA=)
5. Scripts/[__UnityEngine.ECS.SimpleMovement/MoveSpeedComponent__](https://hackmd.io/GYZghgDAnA7AjAVgLQwEwDY5ICxgEbKwRhJQDGeEAHJuCHiEA===)
6. Scripts/[__UnityEngine.ECS.SimpleMovement/MoveAlongCircleComponent__](https://hackmd.io/JwRgxgZgzARgJgBgLQDYoCYAcSAsB2ApTCBbCYPYFAVnWoFMcxqg)
7. Scripts/[__UnityEngine.ECS.SimpleRotation/RotationSpeedResetSphereComponent__](https://hackmd.io/KYBgrALAbARgHHAtMGMBMiIGY0bmgEwxAjgHYQYBjARgDMQ4og==)

Like the RotatingCubeSpawner, The __TransformPositionComponent__ specifies that the entity that is created from the TestResetRotationSphere GameObject has a position in the ECS and the __CopyInitialTransformPositionFromGameObjectComponent__ specifies that **only** the initial value for __TransformPositionComponent__ in ECS will be copied from the GameObject's transform. 

The __TransformMatrixComponent__ specifies that a 4x4 matrix should be stored. That matrix is updated automatically based on changes to the __TransformPositionComponent__.

Set the properties of the __InstanceRendererComponent__
1. Mesh: Sphere
2. Material: InstanceMat
Assign a Material that has GPU Instancing enabled.

This component specfies that this Mesh/Material combination should be rendered with the corresponding __TransformMatrix__ (Required)

Set the properties of the __MoveSpeedComponent__
1. Speed: 1

This component requests that if another component is moving the __TransformPositionComponent__ it should respect this value and move the position at the constant speed specified.

Set the properties of the __MoveAlongCircleComponent__
1. Center: 0,0,0
2. Radius: 25
The center and radius correspond to the circle of entities that is being spawned by RotatingCubeSpawner.

This component will update the corresponding __TransformPositionComponent__ at the rate specified by __MoveSpeedComponent__ in radians per second.

Set the properties of the __RotationSpeedResetSphereComponent__
1. Speed: 4 (radians per second)
2. Radius: 2 (meters)

This component specifies that if any other __TransformPositionComponent__ is within the sphere defined by the __TransformPositionComponent__ on this entity and the radius, the __TransformRotationComponent__ on that entity should be set to speed, if it exists.


















