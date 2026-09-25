using SB4b1.Logging;
using UnityEngine;

public class CharacterHitResolver : DamageReceiver
{
    private CharacterIdentifier charID;

    protected override void Awake()
    {
        base.Awake();
        charID = GetComponentInParent<CharacterIdentifier>();
    }
}
