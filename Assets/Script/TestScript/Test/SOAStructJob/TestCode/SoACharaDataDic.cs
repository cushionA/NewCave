using CharacterController;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using TestScript.SOATest;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using static TestScript.SOATest.SOAStatus;
using MoveStatus = TestScript.SOATest.SOAStatus.MoveStatus;
using SolidData = TestScript.SOATest.SOAStatus.SolidData;

namespace TestScript.Collections
{

    /// <summary>
    /// 固定サイズ・スワップ削除版のキャラクターデータ辞書
    /// 最大容量を事前に確保しリサイズしない
    /// 削除時は削除部分と今の最後の要素を入れ替えることでデータが断片化しない
    /// ハッシュテーブルによりGetComponent不要でデータアクセスが可能
    /// </summary>
    public unsafe class SoACharaDataDic : IDisposable
    {
        #region 定義

        /// <summary>
        /// エントリ構造体 - ハッシュテーブルのエントリ情報
        /// </summary>
        private struct Entry
        {
            /// <summary>
            /// ゲームオブジェクトのハッシュコード（GetInstanceID()と同一）
            /// </summary>
            public int HashCode;

            /// <summary>
            /// データ配列内のインデックス
            /// </summary>
            public int ValueIndex;

            /// <summary>
            /// 同じバケット内の次のエントリへのインデックス（-1は終端）
            /// </summary>
            public int NextInBucket;
        }

        /// <summary>
        /// メモリレイアウト情報
        /// 一括で確保したメモリ内で、それぞれのデータのメモリ配置がどこから始まるかを記録する。
        /// </summary>
        private struct MemoryLayout
        {
            public int BaseInfoOffset;
            public int AtkStatusOffset;
            public int DefStatusOffset;
            public int SolidDataOffset;
            public int StateInfoOffset;
            public int MoveStatusOffset;
            public int ColdLogOffset;
            public int TotalSize;
        }

        #endregion

        #region 定数

        /// <summary>
        /// デフォルトの最大容量
        /// </summary>
        private const int DEFAULT_MAX_CAPACITY = 130;

        /// <summary>
        /// バケット数（ハッシュテーブルのサイズ）
        /// </summary>
        private const int BUCKET_COUNT = 191;  // 素数を使用

        #endregion

        #region フィールド

        /// <summary>
        /// バケット配列（各要素はエントリへのインデックス、-1は空）
        /// </summary>
        private int[] _buckets;

        /// <summary>
        /// エントリのリスト（ハッシュ→インデックスのマッピング）
        /// </summary>
        private UnsafeList<Entry> _entries;

        /// <summary>
        /// ハッシュコードから実際のデータインデックスへのマッピング
        /// スワップ削除時に更新が必要
        /// </summary>
        private int[] _dataIndexToHash;

        /// <summary>
        /// エントリ削除後の使いまわせるスペースのインデックス。
        /// </summary>
        private Stack<int> _freeEntry;

        #region 管理対象のデータ

        /// <summary>
        /// 一括確保したメモリブロック
        /// </summary>
        private byte* _bulkMemory;
        private int _totalMemorySize;

        /// <summary>
        /// キャラクターの基本情報
        /// </summary>
        public UnsafeList<CharacterBaseInfo> _characterBaseInfo;

        /// <summary>
        /// キャラクターの基本情報
        /// </summary>
        public UnsafeList<SolidData> _solidData;

        /// <summary>
        /// 攻撃力のデータ
        /// </summary>
        public UnsafeList<CharacterAtkStatus> _characterAtkStatus;

        /// <summary>
        /// 防御力のデータ
        /// </summary>
        public UnsafeList<CharacterDefStatus> _characterDefStatus;

        /// <summary>
        /// AIが参照するための状態情報
        /// </summary>
        public UnsafeList<CharacterStateInfo> _characterStateInfo;

        /// <summary>
        /// 移動関連のステータス
        /// </summary>
        public UnsafeList<MoveStatus> _moveStatus;

        /// <summary>
        /// 参照頻度の低いデータ
        /// </summary>
        public UnsafeList<CharaColdLog> _coldLog;

        /// <summary>
        /// BaseControllerを格納する配列
        /// </summary>
        private BaseController[] _controllers;

        #endregion

        /// <summary>
        /// 現在の要素数
        /// </summary>
        private int _count;

        /// <summary>
        /// 最大容量
        /// </summary>
        private readonly int _maxCapacity;

        /// <summary>
        /// メモリアロケータ
        /// </summary>
        private readonly Allocator _allocator;

        /// <summary>
        /// 解放済みフラグ
        /// </summary>
        private bool _isDisposed;

        #endregion

        #region プロパティ

        /// <summary>
        /// 現在の要素数
        /// </summary>
        public int Count => this._count;

        /// <summary>
        /// 最大容量
        /// </summary>
        public int MaxCapacity => this._maxCapacity;

        /// <summary>
        /// 使用率（0.0～1.0）
        /// </summary>
        public float UsageRatio => (float)this._count / this._maxCapacity;

        /// <summary>
        /// 今の長さのキャラクターコントローラーを返す。
        /// </summary>
        public Span<BaseController> GetController => this._controllers.AsSpan().Slice(0, this._count);

        #endregion

        #region コンストラクタ

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="maxCapacity">最大容量（デフォルト: 100）</param>
        /// <param name="allocator">メモリアロケータ（デフォルト: Persistent）</param>
        public SoACharaDataDic(int maxCapacity = DEFAULT_MAX_CAPACITY, Allocator allocator = Allocator.Persistent)
        {
            this._maxCapacity = maxCapacity;
            this._allocator = allocator;
            this._count = 0;
            this._isDisposed = false;

            // バケット配列の初期化
            this._buckets = new int[BUCKET_COUNT];
            this._buckets.AsSpan().Fill(-1);

            // エントリリストの初期化
            this._entries = new UnsafeList<Entry>(BUCKET_COUNT * 2, allocator);

            // 削除エントリ保管用のエントリリストも作成。
            this._freeEntry = new Stack<int>(maxCapacity);

            // ハッシュ→データインデックスのマッピング
            this._dataIndexToHash = new int[maxCapacity];
            this._dataIndexToHash.AsSpan().Fill(-1);

            // メモリレイアウトの計算
            MemoryLayout layout = this.CalculateMemoryLayout(maxCapacity);
            this._totalMemorySize = layout.TotalSize;

            // 一括メモリ確保
            this._bulkMemory = (byte*)UnsafeUtility.Malloc(this._totalMemorySize, 64, allocator);
            UnsafeUtility.MemClear(this._bulkMemory, this._totalMemorySize);

            // 各UnsafeListの初期化（固定サイズ）
            this._characterBaseInfo = new UnsafeList<CharacterBaseInfo>(
                (CharacterBaseInfo*)(this._bulkMemory + layout.BaseInfoOffset),
                maxCapacity
            );

            this._characterAtkStatus = new UnsafeList<CharacterAtkStatus>(
                (CharacterAtkStatus*)(this._bulkMemory + layout.AtkStatusOffset),
                maxCapacity
            );

            this._characterDefStatus = new UnsafeList<CharacterDefStatus>(
                (CharacterDefStatus*)(this._bulkMemory + layout.DefStatusOffset),
                maxCapacity
            );

            this._solidData = new UnsafeList<SolidData>(
                (SolidData*)(this._bulkMemory + layout.SolidDataOffset),
                maxCapacity
            );

            this._characterStateInfo = new UnsafeList<CharacterStateInfo>(
                (CharacterStateInfo*)(this._bulkMemory + layout.StateInfoOffset),
                maxCapacity
            );

            this._moveStatus = new UnsafeList<MoveStatus>(
                (MoveStatus*)(this._bulkMemory + layout.MoveStatusOffset),
                maxCapacity
            );

            this._coldLog = new UnsafeList<CharaColdLog>(
                (CharaColdLog*)(this._bulkMemory + layout.ColdLogOffset),
                maxCapacity
            );

            // BaseController配列
            this._controllers = new BaseController[maxCapacity];

            // 長さを初期化
            this._characterBaseInfo.Length = 0;
            this._characterAtkStatus.Length = 0;
            this._characterDefStatus.Length = 0;
            this._solidData.Length = 0;
            this._characterStateInfo.Length = 0;
            this._moveStatus.Length = 0;
            this._coldLog.Length = 0;
        }

        #endregion

        #region 追加・更新

        /// <summary>
        /// ゲームオブジェクトと全キャラクターデータを追加または更新
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Add(GameObject obj, CharacterBaseInfo baseInfo, CharacterAtkStatus atkStatus,
                      CharacterDefStatus defStatus, SolidData solidData, CharacterStateInfo stateInfo,
                      MoveStatus moveStatus, CharaColdLog coldLog, BaseController controller)
        {
            if ( obj == null )
            {
                throw new ArgumentNullException(nameof(obj));
            }

            return this.AddByHash(obj.GetHashCode(), baseInfo, atkStatus, defStatus, solidData,
                           stateInfo, moveStatus, coldLog, controller);
        }

        /// <summary>
        /// ゲームオブジェクトと全キャラクターデータを追加または更新
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Add(GameObject obj, SOAStatus status, BaseController controller)
        {
            if ( obj == null )
            {
                throw new ArgumentNullException(nameof(obj));
            }

            CharacterBaseInfo baseInfo = new(status.baseData, obj.transform.position);
            CharacterAtkStatus atkStatus = new(status.baseData);
            CharacterDefStatus defStatus = new(status.baseData);
            SOAStatus.SolidData solidData = status.solidData;
            CharacterStateInfo stateInfo = new(status.baseData);
            SOAStatus.MoveStatus moveStatus = status.moveStatus;
            CharaColdLog coldLog = new(status, obj);

            return this.AddByHash(obj.GetHashCode(), baseInfo, atkStatus, defStatus, solidData,
                           stateInfo, moveStatus, coldLog, controller);
        }

        /// <summary>
        /// ハッシュコードでデータを追加または更新
        /// </summary>
        public int AddByHash(int hashCode, CharacterBaseInfo baseInfo, CharacterAtkStatus atkStatus,
                            CharacterDefStatus defStatus, SolidData solidData, CharacterStateInfo stateInfo,
                            MoveStatus moveStatus, CharaColdLog coldLog, BaseController controller)
        {

            // 既存エントリの検索
            if ( this.TryGetIndexByHash(hashCode, out int existingIndex) )
            {
                // 更新
                this._characterBaseInfo[existingIndex] = baseInfo;
                this._characterAtkStatus[existingIndex] = atkStatus;
                this._characterDefStatus[existingIndex] = defStatus;
                this._solidData[existingIndex] = solidData;
                this._characterStateInfo[existingIndex] = stateInfo;
                this._moveStatus[existingIndex] = moveStatus;
                this._coldLog[existingIndex] = coldLog;
                this._controllers[existingIndex] = controller;
                return existingIndex;
            }

            // 容量チェック
            if ( this._count >= this._maxCapacity )
            {
                throw new InvalidOperationException($"Maximum capacity ({this._maxCapacity}) exceeded");
            }

            // 新規追加
            int dataIndex = this._count;

            // データを追加
            this._characterBaseInfo.AddNoResize(baseInfo);
            this._characterAtkStatus.AddNoResize(atkStatus);
            this._characterDefStatus.AddNoResize(defStatus);
            this._solidData.AddNoResize(solidData);
            this._characterStateInfo.AddNoResize(stateInfo);
            this._moveStatus.AddNoResize(moveStatus);
            this._coldLog.AddNoResize(coldLog);
            this._controllers[dataIndex] = controller;

            // ハッシュテーブルへの登録
            int bucketIndex = this.GetBucketIndex(hashCode);

            // エントリの追加
            int newEntryIndex;

            // 新しいエントリはバケットの直下に追加される。
            // だから前の直下の要素を NextInBucket に繋げてる。
            Entry newEntry = new()
            {
                HashCode = hashCode,
                ValueIndex = dataIndex,
                NextInBucket = this._buckets[bucketIndex]
            };

            // フリーリストから再利用 or 新規追加
            if ( this._freeEntry.TryPop(out newEntryIndex) )
            {
                this._entries[newEntryIndex] = newEntry;
            }
            // 再利用できない場合は最後尾にエントリ追加
            else
            {
                newEntryIndex = this._entries.Length;
                this._entries.AddNoResize(newEntry);
            }

            // バケットの直下に新エントリを追加
            this._buckets[bucketIndex] = newEntryIndex;

            // マッピングの更新
            this._dataIndexToHash[dataIndex] = hashCode;

            this._count++;

            return dataIndex;
        }

        #endregion

        #region 削除（スワップ削除）

        /// <summary>
        /// ゲームオブジェクトに関連付けられたデータを削除（スワップ削除）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Remove(GameObject obj)
        {
            if ( obj == null )
            {
                return false;
            }

            return this.RemoveByHash(obj.GetHashCode());
        }

        /// <summary>
        /// ハッシュコードに関連付けられたデータを削除（スワップ削除）
        /// O(1)の削除を実現するため、削除対象を最後の要素と入れ替える
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool RemoveByHash(int hashCode)
        {
            if ( !this.TryGetIndexByHash(hashCode, out int dataIndex) )
            {
                return false;
            }

            int lastIndex = this._count - 1;

            // 削除対象が最後の要素でない場合は入れ替え
            if ( dataIndex != lastIndex )
            {
                // 最後の要素を削除位置にコピー
                this._characterBaseInfo[dataIndex] = this._characterBaseInfo[lastIndex];
                this._characterAtkStatus[dataIndex] = this._characterAtkStatus[lastIndex];
                this._characterDefStatus[dataIndex] = this._characterDefStatus[lastIndex];
                this._solidData[dataIndex] = this._solidData[lastIndex];
                this._characterStateInfo[dataIndex] = this._characterStateInfo[lastIndex];
                this._moveStatus[dataIndex] = this._moveStatus[lastIndex];
                this._coldLog[dataIndex] = this._coldLog[lastIndex];
                this._controllers[dataIndex] = this._controllers[lastIndex];

                // 移動した要素のハッシュコードを見つけてマッピングを更新
                this._dataIndexToHash[dataIndex] = this._dataIndexToHash[lastIndex];

                // エントリ内の値インデックスも更新
                this.UpdateEntryDataIndex(this._dataIndexToHash[lastIndex], dataIndex);

            }

            // リストの長さを減らす
            this._characterBaseInfo.Length--;
            this._characterAtkStatus.Length--;
            this._characterDefStatus.Length--;
            this._solidData.Length--;
            this._characterStateInfo.Length--;
            this._moveStatus.Length--;
            this._coldLog.Length--;

            // ハッシュテーブルからエントリを削除
            this.RemoveFromHashTable(hashCode);

            this._count--;
            return true;
        }

        /// <summary>
        /// エントリのデータインデックスを更新。<br/>
        /// 内部データの位置移動に伴い、エントリ内の値インデックスの値を書き換える。<br/>
        /// </summary>
        /// <param name="hashCode">書き換え対象のハッシュ値</param>
        /// <param name="newDataIndex">新しくエントリに割り当てる値の位置</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void UpdateEntryDataIndex(int hashCode, int newDataIndex)
        {
            // 更新対象のエントリがバケットのどの位置にあるかを計算する
            int bucketIndex = this.GetBucketIndex(hashCode);

            // 現在のエントリの開始位置を取得する。
            int entryIndex = this._buckets[bucketIndex];

            // ref参照でエントリを探し、エントリ内の値のインデックスを書き換える。
            while ( entryIndex != -1 )
            {
                ref Entry entry = ref this._entries.ElementAt(entryIndex);

                // ハッシュ値が一致するエントリがあれば値インデックスを書き換える。
                if ( entry.HashCode == hashCode )
                {
                    entry.ValueIndex = newDataIndex;
                    return;
                }

                entryIndex = entry.NextInBucket;
            }
        }

        /// <summary>
        /// ハッシュテーブルからエントリを削除
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void RemoveFromHashTable(int hashCode)
        {
            // 削除対象のエントリがバケットのどの位置にあるかを計算する
            int bucketIndex = this.GetBucketIndex(hashCode);

            // 削除対象のエントリの開始位置を取得する。
            int entryIndex = this._buckets[bucketIndex];

            // 前のエントリが－１の時はバケットの直下のエントリ。
            int prevIndex = -1;

            while ( entryIndex != -1 )
            {
                ref Entry entry = ref this._entries.ElementAt(entryIndex);

                // ハッシュ値が一致するエントリがあれば値インデックスを書き換える
                if ( entry.HashCode == hashCode )
                {
                    // バケット内の一つ目のエントリが削除対象なら、バケットからの参照を直接書き変える。
                    // [削除対象、次エントリ、次々エントリ] というバケットを[次エントリ、次々エントリ]にする 
                    if ( prevIndex == -1 )
                    {
                        this._buckets[bucketIndex] = entry.NextInBucket;
                    }

                    // バケット内で前のエントリから参照されているなら、前のエントリに自分の次のエントリを繋ぎ直す。
                    // [前エントリ、削除対象、次エントリ] というバケットを[前エントリ、次エントリ]にする 
                    else
                    {
                        ref Entry prevEntry = ref this._entries.ElementAt(prevIndex);
                        prevEntry.NextInBucket = entry.NextInBucket;
                    }

                    // 解放されたインデックスをスタックに追加
                    this._freeEntry.Push(entryIndex);
                    return;
                }

                // 一致しなければ前のエントリを記録して次のエントリへ。
                prevIndex = entryIndex;
                entryIndex = entry.NextInBucket;
            }
        }

        #endregion

        #region データ取得

        /// <summary>
        /// ゲームオブジェクトから全データを取得
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetValue(GameObject obj, out CharacterBaseInfo baseInfo, out CharacterAtkStatus atkStatus,
                               out CharacterDefStatus defStatus, out SolidData solidData, out CharacterStateInfo stateInfo,
                               out MoveStatus moveStatus, out CharaColdLog coldLog,
                               out BaseController controller, out int index)
        {
            if ( obj == null )
            {
                baseInfo = default;
                atkStatus = default;
                defStatus = default;
                solidData = default;
                stateInfo = default;
                moveStatus = default;
                coldLog = default;
                controller = null;
                index = -1;
                return false;
            }

            return this.TryGetValueByHash(obj.GetHashCode(), out baseInfo, out atkStatus, out defStatus, out solidData,
                                    out stateInfo, out moveStatus, out coldLog, out controller, out index);
        }

        /// <summary>
        /// ハッシュコードから全データを取得
        /// </summary>
        public bool TryGetValueByHash(int hashCode, out CharacterBaseInfo baseInfo, out CharacterAtkStatus atkStatus,
                                     out CharacterDefStatus defStatus, out SolidData solidData, out CharacterStateInfo stateInfo,
                                     out MoveStatus moveStatus, out CharaColdLog coldLog,
                                     out BaseController controller, out int index)
        {
            if ( this.TryGetIndexByHash(hashCode, out int dataIndex) )
            {
                baseInfo = this._characterBaseInfo[dataIndex];
                atkStatus = this._characterAtkStatus[dataIndex];
                defStatus = this._characterDefStatus[dataIndex];
                solidData = this._solidData[dataIndex];
                stateInfo = this._characterStateInfo[dataIndex];
                moveStatus = this._moveStatus[dataIndex];
                coldLog = this._coldLog[dataIndex];
                controller = this._controllers[dataIndex];
                index = dataIndex;
                return true;
            }

            baseInfo = default;
            atkStatus = default;
            defStatus = default;
            solidData = default;
            stateInfo = default;
            moveStatus = default;
            coldLog = default;
            controller = null;
            index = -1;
            return false;
        }

        /// <summary>
        /// インデックスからデータを直接取得（最高速）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetByIndex(int index, out CharacterBaseInfo baseInfo, out CharacterAtkStatus atkStatus,
                                 out CharacterDefStatus defStatus, out CharacterStateInfo stateInfo, out SolidData solidData,
                                 out MoveStatus moveStatus, out CharaColdLog coldLog, out BaseController controller)
        {
            if ( index < 0 || index >= this._count )
            {
                baseInfo = default;
                atkStatus = default;
                defStatus = default;
                solidData = default;
                stateInfo = default;
                moveStatus = default;
                coldLog = default;
                controller = null;
                return false;
            }

            baseInfo = this._characterBaseInfo[index];
            atkStatus = this._characterAtkStatus[index];
            defStatus = this._characterDefStatus[index];
            solidData = this._solidData[index];
            stateInfo = this._characterStateInfo[index];
            moveStatus = this._moveStatus[index];
            coldLog = this._coldLog[index];
            controller = this._controllers[index];
            return true;
        }

        /// <summary>
        /// ハッシュ値から値のインデックスを取得する。
        /// </summary>
        /// <param name="hashCode"></param>
        /// <param name="index"></param>
        /// <returns></returns>
        public bool TryGetIndexByHash(int hashCode, out int index)
        {
            int bucketIndex = this.GetBucketIndex(hashCode);
            int entryIndex = this._buckets[bucketIndex];

            while ( entryIndex != -1 )
            {
                ref Entry entry = ref this._entries.ElementAt(entryIndex);

                if ( entry.HashCode == hashCode )
                {
                    index = entry.ValueIndex;
                    return true;
                }

                entryIndex = entry.NextInBucket;
            }

            index = -1;
            return false;
        }

        #endregion

        #region 個別データアクセス（参照返し）

        /// <summary>
        /// インデックスからCharacterBaseInfoを取得。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref CharacterBaseInfo GetCharacterBaseInfoByIndex(int index)
        {
            if ( index < 0 || index >= this._count )
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return ref this._characterBaseInfo.ElementAt(index);
        }

        /// <summary>
        /// インデックスからCharacterAtkStatusを取得。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref CharacterAtkStatus GetCharacterAtkStatusByIndex(int index)
        {
            if ( index < 0 || index >= this._count )
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return ref this._characterAtkStatus.ElementAt(index);
        }

        /// <summary>
        /// インデックスからCharacterDefStatusを取得。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref CharacterDefStatus GetCharacterDefStatusByIndex(int index)
        {
            if ( index < 0 || index >= this._count )
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return ref this._characterDefStatus.ElementAt(index);
        }

        /// <summary>
        /// インデックスからCharacterStateInfoを取得。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref SolidData GetSolidDataByIndex(int index)
        {
            if ( index < 0 || index >= this._count )
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return ref this._solidData.ElementAt(index);
        }

        /// <summary>
        /// インデックスからCharacterStateInfoを取得。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref CharacterStateInfo GetCharacterStateInfoByIndex(int index)
        {
            if ( index < 0 || index >= this._count )
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return ref this._characterStateInfo.ElementAt(index);
        }

        /// <summary>
        /// インデックスからMoveStatusを取得。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref MoveStatus GetMoveStatusByIndex(int index)
        {
            if ( index < 0 || index >= this._count )
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return ref this._moveStatus.ElementAt(index);
        }

        /// <summary>
        /// インデックスからCharaColdLogを取得。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref CharaColdLog GetCharaColdLogByIndex(int index)
        {
            if ( index < 0 || index >= this._count )
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return ref this._coldLog.ElementAt(index);
        }

        #endregion

        #region ユーティリティ

        /// <summary>
        /// すべてのエントリをクリア
        /// </summary>
        public void Clear()
        {
            // バケットをリセット
            this._buckets.AsSpan().Fill(-1);

            // エントリとマッピングをクリア
            this._entries.Clear();
            this._dataIndexToHash.AsSpan().Fill(-1);

            // データをクリア（Lengthを0にするだけ）
            this._characterBaseInfo.Length = 0;
            this._characterAtkStatus.Length = 0;
            this._characterDefStatus.Length = 0;
            this._solidData.Length = 0;
            this._characterStateInfo.Length = 0;
            this._moveStatus.Length = 0;
            this._coldLog.Length = 0;

            // コントローラー配列をクリア
            Array.Clear(this._controllers, 0, this._count);

            this._count = 0;
        }

        /// <summary>
        /// 指定したキーが存在するか確認
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ContainsKey(GameObject obj)
        {
            if ( obj == null )
            {
                return false;
            }

            return this.ContainsKeyByHash(obj.GetHashCode());
        }

        /// <summary>
        /// 指定したハッシュコードが存在するか確認
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ContainsKeyByHash(int hashCode)
        {
            return this.TryGetIndexByHash(hashCode, out int _);
        }

        /// <summary>
        /// すべての有効なエントリに対して処理を実行
        /// </summary>
        public void ForEach(Action<int, CharacterBaseInfo, CharacterAtkStatus, CharacterDefStatus, SolidData,
                                  CharacterStateInfo, MoveStatus, CharaColdLog, BaseController> action)
        {
            if ( action == null )
            {
                throw new ArgumentNullException(nameof(action));
            }

            for ( int i = 0; i < this._count; i++ )
            {
                action(i,
                      this._characterBaseInfo[i],
                      this._characterAtkStatus[i],
                      this._characterDefStatus[i],
                      this._solidData[i],
                      this._characterStateInfo[i],
                      this._moveStatus[i],
                      this._coldLog[i],
                      this._controllers[i]);
            }
        }

        #endregion

        #region 内部メソッド

        /// <summary>
        /// メモリレイアウトの計算。<br/>
        /// 各構造体ごとに要素数分のメモリレイアウトを作成する。<br/>
        /// また、各構造体のレイアウトを作成する際は、64Byte区切りで配置されるようにアライメントする。<br/>
        /// 64バイトごとにキャッシュラインが区切られたメモリ空間で、キャッシュしやすい位置にデータを置くために。
        /// </summary>
        private MemoryLayout CalculateMemoryLayout(int capacity)
        {
            MemoryLayout layout = new();
            int currentOffset = 0;

            // CharacterBaseInfo
            layout.BaseInfoOffset = currentOffset;
            currentOffset += capacity * sizeof(CharacterBaseInfo);

            // 64バイト境界にアライメント
            currentOffset = AlignTo(currentOffset, 64);
            layout.AtkStatusOffset = currentOffset;
            currentOffset += capacity * sizeof(CharacterAtkStatus);

            currentOffset = AlignTo(currentOffset, 64);
            layout.DefStatusOffset = currentOffset;
            currentOffset += capacity * sizeof(CharacterDefStatus);

            currentOffset = AlignTo(currentOffset, 64);
            layout.SolidDataOffset = currentOffset;
            currentOffset += capacity * sizeof(SolidData);

            currentOffset = AlignTo(currentOffset, 64);
            layout.StateInfoOffset = currentOffset;
            currentOffset += capacity * sizeof(CharacterStateInfo);

            currentOffset = AlignTo(currentOffset, 64);
            layout.MoveStatusOffset = currentOffset;
            currentOffset += capacity * sizeof(MoveStatus);

            currentOffset = AlignTo(currentOffset, 64);
            layout.ColdLogOffset = currentOffset;
            currentOffset += capacity * sizeof(CharaColdLog);

            currentOffset = AlignTo(currentOffset, 64);
            layout.TotalSize = currentOffset;

            return layout;
        }

        /// <summary>
        /// アライメント計算</br>
        /// 各構造体のレイアウトを作成する際は、64Byte区切りで配置されるようにアライメントする必要がある。<br/>
        /// 64バイトごとにキャッシュラインが区切られたメモリで、キャッシュしやすい位置にデータを置くために。
        /// <param name="memoryOffset">現在のメモリ位置</param>
        /// <param name="alignment">メモリのアライメントの単位。2のべき乗じゃないとだめ</param>
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int AlignTo(int memoryOffset, int alignment)
        {
            // &~ の反転論理積は2のべき乗の alignment の値以下の下位ビットを0にするマスク、つまり alignment の倍数だけを残す(割った余りが全部消えるから)
            // これで引数のオフセットの値に最も近い alignment の倍数を得られる。
            return (memoryOffset + alignment - 1) & ~(alignment - 1);
        }

        /// <summary>
        /// バケットインデックスの計算
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int GetBucketIndex(int hashCode)
        {
            return (hashCode & 0x7FFFFFFF) % BUCKET_COUNT;
        }

        #endregion

        /// <summary>
        /// デコンストラクタによりすべてのデータリストをタプルとして返す
        /// </summary>
        public void Deconstruct(
            out UnsafeList<CharacterBaseInfo> characterBaseInfo,
            out UnsafeList<CharacterAtkStatus> characterAtkStatus,
            out UnsafeList<CharacterDefStatus> characterDefStatus,
            out UnsafeList<SolidData> solidData,
            out UnsafeList<CharacterStateInfo> characterStateInfo,
            out UnsafeList<MoveStatus> moveStatus,
            out UnsafeList<CharaColdLog> coldLog)
        {
            characterBaseInfo = this._characterBaseInfo;
            characterAtkStatus = this._characterAtkStatus;
            characterDefStatus = this._characterDefStatus;
            solidData = this._solidData;
            characterStateInfo = this._characterStateInfo;
            moveStatus = this._moveStatus;
            coldLog = this._coldLog;

        }

        #region IDisposable

        /// <summary>
        /// リソースの解放
        /// </summary>
        public void Dispose()
        {
            if ( this._isDisposed )
            {
                return;
            }

            if ( this._bulkMemory != null )
            {
                UnsafeUtility.Free(this._bulkMemory, this._allocator);
                this._bulkMemory = null;
            }

            this._entries.Dispose();

            this._isDisposed = true;
        }

        #endregion
    }
}