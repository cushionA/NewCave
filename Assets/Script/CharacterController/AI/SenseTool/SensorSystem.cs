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
/// 周辺環境を調査する機能。
/// 戦闘時に敵の攻撃や存在を感知したり、周囲の状況を把握するためのセンサーシステム。
/// 非戦闘時は敵を見つけたら戦闘状態に移行するためのセンサーとしても機能する。
/// 
/// アビリティにはしない
/// </summary>
public class SensorSystem : MonoBehaviour
{
    #region 定義

    /// <summary>
    /// 検知したものを記録するためのデータ
    /// </summary>
    public struct RecognitionLog
    {
        /// <summary>
        /// 認識した位置
        /// </summary>
        public float2 position;

        /// <summary>
        /// 識別対象のタイプ
        /// </summary>
        public RecognizeObjectType objectType;

        /// <summary>
        /// 認識したオブジェクトのハッシュコード。
        /// </summary>
        public int hashCode;

        /// <summary>
        /// 認識データ作成用コンストラクタ
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
    /// 視界として使用するセンサー
    /// 戦闘時は攻撃飛び道具にだけ反応
    /// </summary>
    [SerializeField]
    TriggerSensor2D _sightSensor;

    /// <summary>
    /// 周囲の環境の状況を感知するセンサー。
    /// キャラクターや地形の把握用
    /// これにしばらく敵が入らなければ戦闘終了
    /// </summary>
    [SerializeField]
    RangeSensor2D _environmentSensor;

    /// <summary>
    /// どこまでを至近距離にするかという設定
    /// この距離以内にあるオブジェクトは至近距離として認識される。
    /// キャラオブジェクトの場合は距離を計算
    /// </summary>
    [SerializeField]
    float _closeRangeLimit;

    /// <summary>
    /// environmentSensorが感知する間隔。
    /// この時間の半分の間隔で近距離探知を行う
    /// </summary>
    [SerializeField]
    float _pulseInterval;

    /// <summary>
    /// 敵を感知する数の制限
    /// </summary>
    [SerializeField]
    byte _detectionLimit;

    /// <summary>
    /// 前回のスキャンからの時間を記録する変数。
    /// </summary>
    private float _lastJudgeTime;

    /// <summary>
    /// closeRangeLimitの値を二乗した、範囲検査に実際使用する値。
    /// </summary>
    private float _closeRangeValue;

    /// <summary>
    /// センサー使用時にリストを作成しないためのバッファ
    /// </summary>
    private List<GameObject> _detectBuffer;

    /// <summary>
    /// 認識データのログ
    /// </summary>
    private List<RecognitionLog> _detectLog;

    /// <summary>
    /// 初期設定
    /// バッファの確保と計算に使う値の初期化を行う。
    /// </summary>
    protected void Start()
    {
        _lastJudgeTime = _pulseInterval * -1;
        _closeRangeValue = _closeRangeLimit * _closeRangeLimit;
        _detectBuffer = new List<GameObject>(_detectionLimit);
        _detectLog = new List<RecognitionLog>(_detectionLimit);
        _sightSensor.IgnoreList.Add(gameObject); // 自分自身を無視する
    }

    #region Publicメソッド

    /// <summary>
    /// 戦闘状態かどうかを切り替える。
    /// トリガーセンサーの検出対象が変わる。
    /// より具体的にはフィルターのレイヤーが変更される。
    /// 戦闘時：飛翔体だけを視認する
    /// 非戦闘時：キャラクターと飛翔体を視認する
    /// </summary>
    /// <param name="isCombat"></param>
    public void ModeChange(bool isCombat)
    {
        //_sightSensor.
    }

    /// <summary>
    /// センサーを起動し、周囲の環境を感知する。
    /// </summary>
    /// <param name="recognition"></param>
    public void SensorAct(ref RecognitionData recognition, float nowTime)
    {
        // センサーの実行間隔を確認
        if ( nowTime - _lastJudgeTime < _pulseInterval )
        {
            return;
        }

        // 認識データを初期化
        recognition.Reset();

        // センサー実行
        _environmentSensor.Pulse();

        // サーチ結果のスパンを取得
        Span<GameObject> detectObjects = _environmentSensor.GetDetectionsByDistance(_detectBuffer).AsSpan();

        // 取得したオブジェクトの分析を行う
        DetectDataAnalyze(ref recognition, detectObjects, AIManager.instance.characterDataDictionary.GetPosition(this.gameObject));

        // 最終探索時間を更新
        _lastJudgeTime = nowTime;
    }

    #endregion

    #region Privateメソッド

    /// <summary>
    /// 認識したオブジェクトの仕訳を行う
    /// </summary>
    /// <param name="recognition">認識データ</param>
    [BurstCompile]
    private void DetectDataAnalyze(ref RecognitionData recognition, Span<GameObject> recognizes, float2 myPosition)
    {
        // 認識したオブジェクトを認識データに反映し、認識ログに残して行く。
        for ( int i = 0; i < recognizes.Length; i++ )
        {
            RecognitionLog log = (AIManager.instance.recognizeTagAction[recognizes[i].tag].Invoke(ref recognition, recognizes[i]));
            _detectLog.Add(log);

            // 近距離キャラクターを数える。
            switch ( log.objectType )
            {
                case RecognizeObjectType.プレイヤー側キャラ:
                    if ( math.distancesq(log.position, myPosition) > _closeRangeValue )
                    {
                        continue; // 近距離にいなければスキップ
                    }
                    recognition.nearlyPlayerSideCount++;
                    break;
                case RecognizeObjectType.魔物側キャラ:
                    if ( math.distancesq(log.position, myPosition) > _closeRangeValue )
                    {
                        continue; // 近距離にいなければスキップ
                    }
                    recognition.nearlyMonsterSideCount++;
                    break;
                case RecognizeObjectType.中立側キャラ:
                    if ( math.distancesq(log.position, myPosition) > _closeRangeValue )
                    {
                        continue; // 近距離にいなければスキップ
                    }
                    recognition.nearlyOtherSideCount++;
                    break;
            }
        }
    }

    #endregion
}
