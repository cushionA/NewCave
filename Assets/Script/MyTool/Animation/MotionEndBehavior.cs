using UnityEngine;

public class MotionEndBehavior : StateMachineBehaviour
{
    [Header("�C�x���g�ݒ�")]
    [SerializeField] private string _motionName;
    [SerializeField] private bool _triggerOnExit = true;
    [SerializeField] private bool _triggerOnComplete = true;
    [SerializeField] private float _completeThreshold = 0.95f; // 95%�Ŋ����Ƃ݂Ȃ�

    private bool _hasTriggeredComplete = false;
    private MyMotionAct _targetMotionAct;

    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        _hasTriggeredComplete = false;

        // MyMotionAct�R���|�[�l���g���L���b�V��
        if ( _targetMotionAct == null )
        {
            _targetMotionAct = animator.GetComponent<MyMotionAct>();
        }

        // ���[�V�����J�n��ʒm
        _targetMotionAct?.OnMotionStarted(_motionName, stateInfo);
    }

    public override void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        // normalizedTime�x�[�X�̊������m�i���[�v���Ȃ��A�j���[�V�����p�j
        if ( _triggerOnComplete && !_hasTriggeredComplete && !stateInfo.loop )
        {
            if ( stateInfo.normalizedTime >= _completeThreshold )
            {
                _hasTriggeredComplete = true;
                _targetMotionAct?.OnMotionCompleted(_motionName, stateInfo);
            }
        }

        // �i�s�󋵂̍X�V
        _targetMotionAct?.OnMotionProgress(_motionName, stateInfo.normalizedTime);
    }

    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if ( _triggerOnExit )
        {
            // �����C�x���g���܂����΂��Ă��Ȃ��ꍇ
            if ( !_hasTriggeredComplete )
            {
                _targetMotionAct?.OnMotionInterrupted(_motionName, stateInfo);
            }

            _targetMotionAct?.OnMotionExited(_motionName, stateInfo);
        }
    }
}