using CharacterController.StatusData;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using ToolAttribute.GenContainer;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using static CharacterController.StatusData.BrainStatus;

namespace CharacterController.Collections
{

    /// <summary>
    /// 固定サイズ・スワップ削除版のキャラクターデータ辞書
    /// 最大容量を事前に確保しリサイズしない
    /// 削除時は削除部分と今の最後の要素を入れ替えることでデータが断片化しない
    /// ハッシュテーブルによりGetComponent不要でデータアクセスが可能
    /// </summary>
    [ContainerSetting(
        structType: new[] {
        typeof(CharacterBaseInfo),
        typeof(SolidData),
        typeof(CharacterAtkStatus),
        typeof(CharacterDefStatus),
        typeof(CharacterStateInfo),
        typeof(MoveStatus),
        typeof(CharacterColdLog),
        typeof(RecognitionData),
        },
        classType: new[] {
        typeof(BaseController)
        }
    )]
    public unsafe partial class SoACharaDataDic
    {
        #region プロパティ


        #endregion


        public partial void Dispose();

        #region コレクション操作

        /// <summary>
        /// ゲームオブジェクトと全キャラクターデータを追加または更新
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Add(BrainStatus status, BaseController controller, int hashCode)
        {
            GameObject obj = controller.gameObject;

            if ( obj == null )
            {
                throw new ArgumentNullException(nameof(obj));
            }

            CharacterBaseInfo baseInfo = new(status.baseData, obj.transform.position);
            CharacterAtkStatus atkStatus = new(status.baseData);
            CharacterDefStatus defStatus = new(status.baseData);
            BrainStatus.SolidData solidData = status.solidData;
            CharacterStateInfo stateInfo = new(status.baseData);
            BrainStatus.MoveStatus moveStatus = status.moveStatus;
            CharacterColdLog coldLog = new(status, hashCode);
            RecognitionData recognitionData = new RecognitionData();

            return this.AddByHash(hashCode, baseInfo, solidData, atkStatus, defStatus,
                           stateInfo, moveStatus, coldLog, recognitionData, controller);
        }

        /// <summary>
        /// デコンストラクタによりすべてのstruct型データリストをタプルとして返す
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Deconstruct(
            out UnsafeList<CharacterBaseInfo> characterBaseInfo,
            out UnsafeList<SolidData> solidData,
            out UnsafeList<CharacterAtkStatus> characterAtkStatus,
            out UnsafeList<CharacterDefStatus> characterDefStatus,
            out UnsafeList<CharacterStateInfo> characterStateInfo,
            out UnsafeList<MoveStatus> moveStatus,
            out UnsafeList<CharacterColdLog> characterColdLog,
            out UnsafeList<RecognitionData> recognitionData)
        {
            characterBaseInfo = this._characterBaseInfo;
            solidData = this._solidData;
            characterAtkStatus = this._characterAtkStatus;
            characterDefStatus = this._characterDefStatus;
            characterStateInfo = this._characterStateInfo;
            moveStatus = this._moveStatus;
            characterColdLog = this._characterColdLog;
            recognitionData = this._recognitionData;
        }

        #endregion

        #region データ取得

        /// <summary>
        /// キャラの所属を取得するメソッド
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public CharacterBelong GetBelong(GameObject obj)
        {
            return TryGetIndexByHash(obj.GetHashCode(), out int index)
                ? _characterStateInfo[index].belong
                : CharacterBelong.指定なし;
        }

        /// <summary>
        /// キャラの位置を取得するメソッド
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public float2 GetPosition(GameObject obj)
        {
            return TryGetIndexByHash(obj.GetHashCode(), out int index)
                ? _characterBaseInfo[index].nowPosition
                : float2.zero;
        }

        #endregion

        #region データ更新

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        /// <summary>
        /// 新規判断時の処理
        /// 判断完了後にクールタイムなどをアップデートする。
        /// </summary>
        public void UpdateDataAfterJudge(int hashCode, ActState newAct, int actionNum, float judgeTime)
        {
            if ( TryGetIndexByHash(hashCode, out int index) )
            {
                int id = _characterColdLog[index].characterID;

                CharacterStateInfo stateInfo = _characterStateInfo[index];
                stateInfo.actState = newAct;
                _characterStateInfo[index] = stateInfo;

                CharacterColdLog coldLog = _characterColdLog[index];

                coldLog.lastMoveJudgeTime = judgeTime;

                // 有効な新規判断をしている場合。
                if ( actionNum != -1 )
                {
                    coldLog.lastJudgeTime = judgeTime;
                    coldLog.nowCoolTime = AIManager.instance.brainStatusList.brainArray[id - 1].brainSetting[(int)newAct].behaviorSetting[actionNum].coolTimeData;
                }

                _characterColdLog[index] = coldLog;
            }

            // 存在しなければ処理しない。
            return;
        }

        #endregion


    }
}