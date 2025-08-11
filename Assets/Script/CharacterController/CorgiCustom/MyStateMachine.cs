using CharacterController;
using Codice.CM.Common;
using MoreMountains.CorgiEngine;
using MoreMountains.Tools;
using UnityEngine;

/// <summary>
/// MMStateMachineのオーバーライド
/// キャラクターの状態を管理するための状態マシン。
/// AIとリンクして状態の管理場所を一か所にする。
/// </summary>
public class MyConditionStateMachine : MMStateMachine<CharacterStates.CharacterConditions>
{
    /// <summary>
    /// 自分のハッシュ値
    /// </summary>
    private int _myHash;

    /// <summary>
    /// 継承元のコンストラクタに値を渡すためのオーバーライド。
    /// </summary>
    /// <param name="target"></param>
    /// <param name="triggerEvents"></param>
    public MyConditionStateMachine(GameObject target, bool triggerEvents) : base(target, triggerEvents)
    {
        _myHash = target.GetHashCode();
    }

    /// <summary>
    /// 現在のキャラクターの状態
    /// AI仕様とリンクさせるためのオーバーライド
    /// </summary>
    public override CharacterStates.CharacterConditions CurrentState
    {
        get
        {
            return AIManager.instance.characterDataDictionary.GetCharacterState(_myHash);
        }
        protected set
        {
            AIManager.instance.characterDataDictionary.SetCharacterState(_myHash, value);
        }
    }
}