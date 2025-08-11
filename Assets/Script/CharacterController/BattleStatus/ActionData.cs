using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// �S�Ă̍s���̊��N���X
/// ���[�V�����A�ړ������A�ڐG���[�h������
/// �G�t�F�N�g�֘A�͕ʂɎ��B
/// </summary>
public class ActionData
{
    /// <summary>
    /// ���[�V�����ړ��̏�Ԃ�\���񋓌^�B
    /// 
    /// </summary>
    public enum RushState : byte
    {
        ��~,
        �ҋ@,
        �ړ�
    }

    /// <summary>
    /// ���[�V�����ړ����A�G�ƐڐG�����ۂ̍s���B
    /// </summary>
    public enum MoveContactType : byte
    {
        �ʉ�,//�ʂ蔲����B�ڐG�Ȃ�
        ��~,//�G�ƐڐG������~�܂�
        ����//�G�������Đi��ł���
    }

    /// <summary>
    /// �A�N�V�����̓���
    /// �A�j���C�x���g�Ŏw�肵�����ԂŎn��
    /// </summary>
    public enum ActionFeature : byte
    {
        ���G,
        �X�[�p�[�A�[�}�[,// �U���A�[�}�[�Ƃ͈قȂ芮�S�ɋ��܂Ȃ�
    }

    /// <summary>
    /// �A�N�V�������̈ړ��̊ɋ}�ɂ��Ă̐ݒ�
    /// </summary>
    public enum MoveSpeedType : byte
    {
        ���� = 0,
        ������,
        �w���I�ɉ���,
        �w���I�ɉ�����Ɍ���,
        ������蓮������}����,
    }


    [Header("�A�N�V�����ړ��̎���")]
    /// <summary>
    /// �ړ����鎞��
    /// </summary>
    public float moveDuration;

    [Header("�A�N�V�����ړ��̋���")]
    /// <summary>
    /// �U���̈ړ�����
    /// ���b�N�I������ꍇ�͂��͈͓̔��œG�Ƃ̋���������
    /// ���x�͈ړ����������ԂŊ���������
    /// </summary>
    public float2 moveDistance;

    [Header("�G�L�����ƐڐG���̋���")]
    /// <summary>
    /// �s���ړ����ɓG�ƐڐG���̋���
    /// </summary>
    public MoveContactType contactType;

    [Header("�ړ����J�n����܂ł̎���")]
    /// <summary>
    /// �ړ��J�n����܂ł̎���
    /// </summary>
    public float startMoveTime;

    /// <summary>
    /// �A�N�V�������̈ړ����x�̓���
    /// </summary>
    [Header("�A�N�V�������̈ړ����x�̓���")]
    public MoveSpeedType speedType;

    [Header("���b�N�I�����邩")]
    /// <summary>
    /// ���b�N�I�����Ĉړ����邩�ǂ���
    /// ���b�N�I��������ړ��������L�т��肵�ĉ��Ƃ��Ăł��^�[�Q�b�g�̑O�ɍs��
    /// �^�[�Q�b�g�͌�������͂��Ȃ̂�
    /// </summary>
    public bool lockAction;

    /// <summary>
    /// �A�[�}�[
    /// </summary>
    [Header("�s�����̒ǉ��A�[�}�[")]
    public int additionalArmor;//g

    [Header("���[�V����")]
    /// <summary>
    /// �A�N�V�����Ŏg�p���郂�[�V�����B
    /// </summary>
    public Animation motionAnime;

    [Header("���̃A�N�V����")]
    /// <summary>
    /// ���Ƀ`�F�C������A�N�V����
    /// �W�Q����Ȃ���Ύ����Ŏ��s����B
    /// </summary>
    public ActionData nextAction;
}
