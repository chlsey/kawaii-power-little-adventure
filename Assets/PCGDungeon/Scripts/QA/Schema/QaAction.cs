using System;
using UnityEngine;

// Mirrors qa_framework's QAAction contract: per-axis movement clamped to
// [-1, 1], boolean attack/interact, and a nonnegative ability slot.
[Serializable]
public struct QaAction
{
    public readonly float MoveX;
    public readonly float MoveY;
    public readonly bool Attack;
    public readonly bool Interact;
    public readonly int Ability;

    public QaAction(float moveX, float moveY, bool attack, bool interact, int ability)
    {
        MoveX = Mathf.Clamp(moveX, -1f, 1f);
        MoveY = Mathf.Clamp(moveY, -1f, 1f);
        Attack = attack;
        Interact = interact;
        Ability = Mathf.Max(0, ability);
    }

    public static QaAction Noop => new QaAction(0f, 0f, false, false, 0);
}
