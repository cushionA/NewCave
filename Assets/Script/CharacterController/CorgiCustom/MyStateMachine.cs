using CharacterController;
using Codice.CM.Common;
using MoreMountains.CorgiEngine;
using MoreMountains.Tools;
using UnityEngine;

/// <summary>
/// MMStateMachine�̃I�[�o�[���C�h
/// �L�����N�^�[�̏�Ԃ��Ǘ����邽�߂̏�ԃ}�V���B
/// AI�ƃ����N���ď�Ԃ̊Ǘ��ꏊ���ꂩ���ɂ���B
/// </summary>
public class MyConditionStateMachine : MMStateMachine<CharacterStates.CharacterConditions>
{
    /// <summary>
    /// �����̃n�b�V���l
    /// </summary>
    private int _myHash;

    /// <summary>
    /// �p�����̃R���X�g���N�^�ɒl��n�����߂̃I�[�o�[���C�h�B
    /// </summary>
    /// <param name="target"></param>
    /// <param name="triggerEvents"></param>
    public MyConditionStateMachine(GameObject target, bool triggerEvents) : base(target, triggerEvents)
    {
        _myHash = target.GetHashCode();
    }

    /// <summary>
    /// ���݂̃L�����N�^�[�̏��
    /// AI�d�l�ƃ����N�����邽�߂̃I�[�o�[���C�h
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