using CharacterController;
using Micosmo.SensorToolkit;
using System;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using static CharacterController.StatusData.BrainStatus;

/// <summary>
/// ���ӊ��𒲍�����@�\�B
/// �퓬���ɓG�̍U���⑶�݂����m������A���͂̏󋵂�c�����邽�߂̃Z���T�[�V�X�e���B
/// ��퓬���͓G����������퓬��ԂɈڍs���邽�߂̃Z���T�[�Ƃ��Ă��@�\����B
/// 
/// �A�r���e�B�ɂ͂��Ȃ�
/// </summary>
public class SensorSystem : MonoBehaviour
{
    #region ��`

    /// <summary>
    /// ���m�������̂��L�^���邽�߂̃f�[�^
    /// </summary>
    public struct RecognitionLog
    {
        /// <summary>
        /// �F�������ʒu
        /// </summary>
        public float2 position;

        /// <summary>
        /// ���ʑΏۂ̃^�C�v
        /// </summary>
        public RecognizeObjectType objectType;

        /// <summary>
        /// �F�������I�u�W�F�N�g�̃n�b�V���R�[�h�B
        /// </summary>
        public int hashCode;

        /// <summary>
        /// �F���f�[�^�쐬�p�R���X�g���N�^
        /// </summary>
        public RecognitionLog(GameObject target, RecognizeObjectType type)
        {
            position = (Vector2)target.transform.position;
            objectType = type;
            hashCode = target.GetHashCode();
        }
    }

    #endregion

    /// <summary>
    /// ���E�Ƃ��Ďg�p����Z���T�[
    /// �퓬���͍U����ѓ���ɂ�������
    /// </summary>
    [SerializeField]
    TriggerSensor2D _sightSensor;

    /// <summary>
    /// ���͂̊��̏󋵂����m����Z���T�[�B
    /// �L�����N�^�[��n�`�̔c���p
    /// ����ɂ��΂炭�G������Ȃ���ΐ퓬�I��
    /// </summary>
    [SerializeField]
    RangeSensor2D _environmentSensor;

    /// <summary>
    /// �ǂ��܂ł����ߋ����ɂ��邩�Ƃ����ݒ�
    /// ���̋����ȓ��ɂ���I�u�W�F�N�g�͎��ߋ����Ƃ��ĔF�������B
    /// �L�����I�u�W�F�N�g�̏ꍇ�͋������v�Z
    /// </summary>
    [SerializeField]
    float _closeRangeLimit;

    /// <summary>
    /// environmentSensor�����m����Ԋu�B
    /// ���̎��Ԃ̔����̊Ԋu�ŋߋ����T�m���s��
    /// </summary>
    [SerializeField]
    float _pulseInterval;

    /// <summary>
    /// �G�����m���鐔�̐���
    /// </summary>
    [SerializeField]
    byte _detectionLimit;

    /// <summary>
    /// �O��̃X�L��������̎��Ԃ��L�^����ϐ��B
    /// </summary>
    private float _lastJudgeTime;

    /// <summary>
    /// closeRangeLimit�̒l���悵���A�͈͌����Ɏ��ێg�p����l�B
    /// </summary>
    private float _closeRangeValue;

    /// <summary>
    /// �Z���T�[�g�p���Ƀ��X�g���쐬���Ȃ����߂̃o�b�t�@
    /// </summary>
    private List<GameObject> _detectBuffer;

    /// <summary>
    /// �F���f�[�^�̃��O
    /// </summary>
    private List<RecognitionLog> _detectLog;

    /// <summary>
    /// �����ݒ�
    /// �o�b�t�@�̊m�ۂƌv�Z�Ɏg���l�̏��������s���B
    /// </summary>
    protected void Start()
    {
        _lastJudgeTime = _pulseInterval * -1;
        _closeRangeValue = _closeRangeLimit * _closeRangeLimit;
        _detectBuffer = new List<GameObject>(_detectionLimit);
        _detectLog = new List<RecognitionLog>(_detectionLimit);
        _sightSensor.IgnoreList.Add(gameObject); // �������g�𖳎�����
    }

    #region Public���\�b�h

    /// <summary>
    /// �퓬��Ԃ��ǂ�����؂�ւ���B
    /// �g���K�[�Z���T�[�̌��o�Ώۂ��ς��B
    /// ����̓I�ɂ̓t�B���^�[�̃��C���[���ύX�����B
    /// �퓬���F���đ̂��������F����
    /// ��퓬���F�L�����N�^�[�Ɣ��đ̂����F����
    /// </summary>
    /// <param name="isCombat"></param>
    public void ModeChange(bool isCombat)
    {
        //_sightSensor.
    }

    /// <summary>
    /// �Z���T�[���N�����A���͂̊������m����B
    /// </summary>
    /// <param name="recognition"></param>
    public void SensorAct(ref RecognitionData recognition, float nowTime)
    {
        // �Z���T�[�̎��s�Ԋu���m�F
        if ( nowTime - _lastJudgeTime < _pulseInterval )
        {
            return;
        }

        // �F���f�[�^��������
        recognition.Reset();

        // �Z���T�[���s
        _environmentSensor.Pulse();

        // �T�[�`���ʂ̃X�p�����擾
        Span<GameObject> detectObjects = _environmentSensor.GetDetectionsByDistance(_detectBuffer).AsSpan();

        // �擾�����I�u�W�F�N�g�̕��͂��s��
        DetectDataAnalyze(ref recognition, detectObjects, AIManager.instance.characterDataDictionary.GetPosition(this.gameObject));

        // �ŏI�T�����Ԃ��X�V
        _lastJudgeTime = nowTime;
    }

    #endregion

    #region Private���\�b�h

    /// <summary>
    /// �F�������I�u�W�F�N�g�̎d����s��
    /// </summary>
    /// <param name="recognition">�F���f�[�^</param>
    [BurstCompile]
    private void DetectDataAnalyze(ref RecognitionData recognition, Span<GameObject> recognizes, float2 myPosition)
    {
        // �F�������I�u�W�F�N�g��F���f�[�^�ɔ��f���A�F�����O�Ɏc���čs���B
        for ( int i = 0; i < recognizes.Length; i++ )
        {
            RecognitionLog log = (AIManager.instance.recognizeTagAction[recognizes[i].tag].Invoke(ref recognition, recognizes[i]));
            _detectLog.Add(log);

            // �ߋ����L�����N�^�[�𐔂���B
            switch ( log.objectType )
            {
                case RecognizeObjectType.�v���C���[���L����:
                    if ( math.distancesq(log.position, myPosition) > _closeRangeValue )
                    {
                        continue; // �ߋ����ɂ��Ȃ���΃X�L�b�v
                    }
                    recognition.nearlyPlayerSideCount++;
                    break;
                case RecognizeObjectType.�������L����:
                    if ( math.distancesq(log.position, myPosition) > _closeRangeValue )
                    {
                        continue; // �ߋ����ɂ��Ȃ���΃X�L�b�v
                    }
                    recognition.nearlyMonsterSideCount++;
                    break;
                case RecognizeObjectType.�������L����:
                    if ( math.distancesq(log.position, myPosition) > _closeRangeValue )
                    {
                        continue; // �ߋ����ɂ��Ȃ���΃X�L�b�v
                    }
                    recognition.nearlyOtherSideCount++;
                    break;
            }
        }
    }

    #endregion
}
