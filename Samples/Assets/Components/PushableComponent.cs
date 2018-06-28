using System;
using Unity.Entities;
using Unity.Mathematics;

public struct Pushable : IComponentData{}

public class PushableComponent : ComponentDataWrapper<Pushable> { }