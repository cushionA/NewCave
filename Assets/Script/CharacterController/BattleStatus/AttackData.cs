using CharacterController;
using MoreMountains.CorgiEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static CharacterController.StatusData.BrainStatus;


/// <summary>
/// �\���̂ɂ���
/// �\���̂Ȃ�l�����ł��邩��ǐ��オ��
/// 
/// ���@��U���A�N�V�����Ȃǂ̏��ׁ̂[�X�ɂȂ����
/// </summary>
public class AttackData : ActionData
{
    #region ��`

    #region enum

    /// <summary>
    /// �s�����Ƃɓ���ւ���K�v�̂���f�[�^
    /// �Ƃ�킯�_���[�W�v�Z�Ɏg�����̂̍\����
    /// ��Ԉُ���ǂ��ɂ����Ȃ��Ƃ�
    /// 
    /// Health���ł͐ڐG���ɑ����Ă����̂��_���[�W�X�e�[�^�X���A�񕜃X�e�[�^�X���œ����ς���悤�ɂ���
    /// </summary>
    public class DamageStatus
    {

        [Header("�U���̃��C������")]
        public Element mainElement;//g

        /// <summary>
        /// �_���[�W�v�Z�Ŏg��
        /// </summary>
        [Header("�U���̑S����")]
        public Element useElement;//g

        /// <summary>
        /// ���[�V�����l
        /// </summary>
        [Header("���[�V�����l")]
        public float mValue;//g

        /// <summary>
        /// �A�[�}�[���
        /// </summary>
        [Header("�A�[�}�[���")]
        public int shock;//g

        /// <summary>
        /// ������΂���
        /// ���ꂪ0�ȏ�Ȃ琁����΂��U�����s��
        /// </summary>
        [Header("������΂���")]
        public Vector2 blowPower;//g

        /// <summary>
        /// �p���B�̃A�[�}�[���ɑ΂����R
        /// </summary>
        [Header("�p���B�̃A�[�}�[���ɑ΂����R")]
        public float parryResist;

        /// <summary>
        /// �q�b�g�񐔐���
        /// </summary>
        [Header("�q�b�g�񐔐���")]
        public byte hitLimit;//g

        /// <summary>
        /// �U�����̂̐���
        /// </summary>
        public AttackFeature attackFeature;
    }

    /// <summary>
    /// �U�����[�V�����̐���
    /// </summary>
    [Flags]
    public enum AttackMoveFeature : byte
    {
        �y���U�� = 1 << 0,// �e�����
        �d���U�� = 1 << 1,
        �O���K�[�h�U�� = 1 << 2,// �K�[�h�U���B�A�j���C�x���g�Ńg���K�[�����B
        ����K�[�h�U�� = 1 << 3,
        �����U�� = 1 << 4,
        �^���\ = 1 << 5,// �G�������U��
        �Ȃ� = 0
    }


    /// <summary>
    /// �U������̐���
    /// �U�����̂��̂����A�_���[�W�v�Z���ɉe�����鐫��
    /// </summary>
    [Flags]
    public enum AttackFeature : byte
    {
        �p���B�s�� = 1 << 0,
        �q�b�g��HP�h���C�� = 1 << 1,//��������������
        �q�b�g��MP�h���C�� = 1 << 2,//������������MP�񕜁B
        �Ȃ� = 0
    }

    /// <summary>
    /// �A�N�V�����̋��x
    /// �G�t�F�N�g�̔��f�Ɏg��
    /// </summary>
    public enum AttackLevel : byte
    {
        ��U��,
        �ʏ�U��,
        ���U��,
        �K�E,
        ����G�t�F�N�g,
        �ˏo���[�V����//�����ʒe�ێg�p���[�V����
    }

    #endregion

    #region �N���X

    #endregion �N���X

    #endregion

    #region ���[�V�����Đ��֘A

    /// <summary>
    /// �ǂ�ȃG�t�F�N�g�≹�����炤��
    /// </summary>
    [Header("�U���G�t�F�N�g�̃��x��")]
    public AttackLevel effectLevel;

    /// <summary>
    /// �p���B����邩�A�y�����d�����Ȃ�
    /// </summary>
    [Header("�U���̐���")]
    public AttackMoveFeature feature;

    [Header("�G�ɑ΂��čU����������")]
    /// <summary>
    /// �łƂ���
    /// </summary>
    public EffectData attackMotionEvent;

    #endregion

    /// <summary>
    /// ����̓������܂ނ����`�F�b�N
    /// </summary>
    /// <param name="checkCondition"></param>
    /// <returns></returns>
    public bool CheckFeature(AttackMoveFeature checkCondition)
    {
        return (feature & checkCondition) > 0;
    }
}
