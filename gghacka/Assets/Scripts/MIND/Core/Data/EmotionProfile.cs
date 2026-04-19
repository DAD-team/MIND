using System;
using System.Collections.Generic;

namespace MIND.Core
{
    [Serializable]
    public class EmotionProfile
    {
        public int phq9Score;
        public float duchenneSmileProxy;
        public float flatAffectScore;
        public float earRatio;
        public float headPitch;
        public float silenceHours;
        public string lastIntervention;
        public List<string> academicEvents = new();

        public PhqSeverity GetSeverity()
        {
            if (phq9Score >= 15) return PhqSeverity.Severe;
            if (phq9Score >= 10) return PhqSeverity.Moderate;
            if (phq9Score >= 5) return PhqSeverity.Mild;
            return PhqSeverity.Minimal;
        }
    }

    public enum PhqSeverity
    {
        Minimal,
        Mild,
        Moderate,
        Severe
    }
}
