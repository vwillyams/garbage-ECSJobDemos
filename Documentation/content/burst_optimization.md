# burst_optimization
# How to Optimize for the Burst Compiler

* Use Unity.Mathematics, burst natively understands the math operations and is optimized for it.
* Avoid branches Use math.min, math.max, math.select instead.
* For jobs that have to be highly optimized, ensure that each job uses every single variable in the IComponentData. If some variables in an IComponentData is not being used, move it to a seperate component. So that the unused data will not be loaded into cachelines when iterating over entities.