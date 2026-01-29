using CrashKonijn.Goap.Runtime;
using UnityEngine;

public abstract class AIActionEvaluator  : ScriptableObject
{
    public abstract float Evaluate(BaseAI context);
    public abstract void Execute(GoapActionProvider ai);
}
