using System;

namespace SqueakSqueak.Experiment
{
    /// <summary>HUD/목표의 단일 계산. 초기 합0과 미초기화를 구분합니다.</summary>
    public readonly struct ExperimentProgress
    {
        public bool IsValid { get; }
        public bool IsComplete { get; }
        public double Value { get; }
        public int DisplayPercent => IsComplete ? 100 : Math.Min(99, (int)Math.Round(Value * 100));
        public ExperimentProgress(bool prepared, int initialA, int removedA, double initialB, double currentB,double initialSide=0,double currentSide=0)
        {
            IsValid = prepared && initialA >= 0 && removedA >= 0 && removedA <= initialA &&
                !double.IsNaN(initialB) && !double.IsInfinity(initialB) && !double.IsNaN(currentB) && !double.IsInfinity(currentB) &&
                initialB >= 0 && currentB >= 0 && currentB <= initialB&&double.IsFinite(initialSide)&&double.IsFinite(currentSide)&&initialSide>=0&&currentSide>=0&&currentSide<=initialSide;
            IsComplete = false; Value = 0;
            if (!IsValid) return;
            var count = 0; double sum = 0; var complete = true;
            if (initialA > 0) { count++; sum += (double)removedA / initialA; complete &= removedA == initialA; }
            if (initialB > 0)
            {
                count++; var residual = currentB / initialB;
                sum += Math.Max(0, Math.Min(1, (1 - residual) / .95d));
                complete &= residual <= .05d + 1e-6d;
            }
            IsComplete = complete;
            if(initialSide>0)IsComplete&=currentSide/initialSide<=.05d+1e-6d;
            Value = count == 0 ? 1 : sum / count;
        }
    }
}
