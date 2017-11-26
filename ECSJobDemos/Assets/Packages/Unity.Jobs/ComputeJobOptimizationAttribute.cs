namespace Unity.Jobs
{
    public enum Support
    {
        Strict,
        Relaxed
    }

    public enum Accuracy
    {
        Low,
        Med,
        High,
        Std
    }

    public class ComputeJobOptimizationAttribute : System.Attribute
    {
        internal Support m_Support;
        internal Accuracy m_Accuracy;

        public ComputeJobOptimizationAttribute()
        {
            m_Support = Support.Strict;
            m_Accuracy = Accuracy.Std;
        }

        public ComputeJobOptimizationAttribute(Accuracy accuracy, Support support)
        {
            m_Support = support;
            m_Accuracy = accuracy;
        }
    }
}