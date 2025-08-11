using CharacterController.StatusData;
using MoreMountains.CorgiEngine;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using ToolAttribute.GenContainer;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using static CharacterController.BaseController;
using static CharacterController.StatusData.BrainStatus;
using static MoreMountains.CorgiEngine.MyCharacter;

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
        typeof(MovementInfo)
        },
        classType: new[] {
        typeof(MyCharacter)
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
        public int Add(BrainStatus status, MyCharacter controller, int hashCode)
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
                           stateInfo, moveStatus, coldLog, recognitionData, new MovementInfo(), controller);
        }

        /// <summary>
        /// すべてのstruct型データリストをタプルとして返す
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public (UnsafeList<CharacterBaseInfo> characterBaseInfo,
                UnsafeList<CharacterAtkStatus> characterAtkStatus,
                UnsafeList<CharacterDefStatus> characterDefStatus,
                UnsafeList<SolidData> solidData,
                UnsafeList<CharacterStateInfo> characterStateInfo,
                UnsafeList<MoveStatus> moveStatus,
                UnsafeList<CharacterColdLog> coldLog,
                UnsafeList<RecognitionData> recognizeData,
                UnsafeList<MovementInfo> judgeResult) GetAllData()
        {
            return (_characterBaseInfo,
                    _characterAtkStatus,
                    _characterDefStatus,
                    _solidData,
                    _characterStateInfo,
                    _moveStatus,
                    _characterColdLog,
                    _recognitionData,
                    _movementInfo);
        }

        #endregion

        #region データ取得

        /// <summary>
        /// キャラの所属を取得するメソッド
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float2 GetPosition(GameObject obj)
        {
            return TryGetIndexByHash(obj.GetHashCode(), out int index)
                ? _characterBaseInfo[index].nowPosition
                : float2.zero;
        }

        /// <summary>
        /// キャラの位置を取得するメソッド
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float2 GetPosition(int hash)
        {
            return TryGetIndexByHash(hash, out int index)
                ? _characterBaseInfo[index].nowPosition
                : float2.zero;
        }

        /// <summary>
        /// あるキャラのターゲットのハッシュを返すメソッド
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetTargetHash(GameObject obj)
        {
            return TryGetIndexByHash(obj.GetHashCode(), out int index)
                ? _movementInfo[index].targetHash
                : -1;
        }

        /// <summary>
        /// あるキャラのターゲットのハッシュを返すメソッド
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetTargetHash(int hash)
        {
            return TryGetIndexByHash(hash, out int index)
                ? _movementInfo[index].targetHash
                : -1;
        }

        /// <summary>
        /// 現在の状態をハッシュ値で取得するメソッド
        /// </summary>
        /// <param name="hash"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public CharacterStates.CharacterConditions GetCharacterState(int hash)
        {
            return TryGetIndexByHash(hash, out int index)
                ? _characterStateInfo[index].conditionState
                : CharacterStates.CharacterConditions.Normal;
        }

        /// <summary>
        /// 現在の状態をハッシュ値でセットするためのメソッド
        /// </summary>
        /// <param name="hash"></param>
        /// <param name="value"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetCharacterState(int hash, CharacterStates.CharacterConditions value)
        {
            TryGetIndexByHash(hash, out int index);
            if ( index < 0 )
            {
                return; // 存在しない場合は何もしない
            }

            // 見つかった場合は状態を更新
            this._characterStateInfo.ElementAt(index).conditionState = value;
        }

        /// <summary>
        /// オブジェクトのハッシュからキャラクターを取得するメソッド
        /// </summary>
        /// <param name="hash"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public MyCharacter GetCharacterByHash(int hash)
        {
            if ( TryGetIndexByHash(hash, out int index) )
            {
                return _myCharacters[index];
            }
            return null; // 存在しない場合はnullを返す
        }

        #endregion

        #region データ更新

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        /// <summary>
        /// 新規判断時の処理
        /// 判断完了後にクールタイムなどをアップデートする。
        /// </summary>
        public void UpdateDataAfterJudge(int hashCode, int actionNum, JudgeResult result, float judgeTime)
        {
            if ( TryGetIndexByHash(hashCode, out int index) )
            {
                int id = _characterColdLog[index].characterID;

                CharacterStateInfo stateInfo = _characterStateInfo[index];
                _characterStateInfo[index] = stateInfo;

                CharacterColdLog coldLog = _characterColdLog[index];

                ///coldLog.lastMoveJudgeTime = judgeTime;

                // 有効な新規判断をしている場合。
                if ( actionNum != -1 )
                {
                    coldLog.lastJudgeTime = judgeTime;
                    //    coldLog.nowCoolTime = AIManager.instance.brainStatusList.brainArray[id - 1].brainSetting[(int)newAct].behaviorSetting[actionNum].coolTimeData;
                }

                _characterColdLog[index] = coldLog;
            }

            // 存在しなければ処理しない。
            return;
        }

        #endregion


    }
}