using UnityEngine;

public class MotionEndBehavior : StateMachineBehaviour
{
    [Header("イベント設定")]
    [SerializeField] private string _motionName;
    [SerializeField] private bool _triggerOnExit = true;
    [SerializeField] private bool _triggerOnComplete = true;
    [SerializeField] private float _completeThreshold = 0.95f; // 95%で完了とみなす

    private bool _hasTriggeredComplete = false;
    private MyMotionAct _targetMotionAct;

    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        _hasTriggeredComplete = false;

        // MyMotionActコンポーネントをキャッシュ
        if ( _targetMotionAct == null )
        {
            _targetMotionAct = animator.GetComponent<MyMotionAct>();
        }

        // モーション開始を通知
        _targetMotionAct?.OnMotionStarted(_motionName, stateInfo);
    }

    public override void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        // normalizedTimeベースの完了検知（ループしないアニメーション用）
        if ( _triggerOnComplete && !_hasTriggeredComplete && !stateInfo.loop )
        {
            if ( stateInfo.normalizedTime >= _completeThreshold )
            {
                _hasTriggeredComplete = true;
                _targetMotionAct?.OnMotionCompleted(_motionName, stateInfo);
            }
        }

        // 進行状況の更新
        _targetMotionAct?.OnMotionProgress(_motionName, stateInfo.normalizedTime);
    }

    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if ( _triggerOnExit )
        {
            // 完了イベントがまだ発火していない場合
            if ( !_hasTriggeredComplete )
            {
                _targetMotionAct?.OnMotionInterrupted(_motionName, stateInfo);
            }

            _targetMotionAct?.OnMotionExited(_motionName, stateInfo);
        }
    }
}