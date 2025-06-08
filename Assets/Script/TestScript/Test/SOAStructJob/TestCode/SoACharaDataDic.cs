using CharacterController;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using static TestScript.SOATest.SOAStatus;

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

        /// <summary>
        /// 個人のヘイトを管理するための構造体
        /// ネイティブアレイで運用する。
        /// </summary>
        public struct PersonalHateContainer
        {
            public NativeHashMap<int, int> personalHate;

            public PersonalHateContainer(int count = 5)
            {
                personalHate = new NativeHashMap<int, int>(count, Allocator.Persistent);
            }
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
        /// キャラごとの個人ヘイト管理用
        /// </summary>
        public NativeArray<PersonalHateContainer> _pHate;

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
        public int Count => _count;

        /// <summary>
        /// 最大容量
        /// </summary>
        public int MaxCapacity => _maxCapacity;

        /// <summary>
        /// 使用率（0.0～1.0）
        /// </summary>
        public float UsageRatio => (float)_count / _maxCapacity;

        /// <summary>
        /// 今の長さのキャラクターコントローラーを返す。
        /// </summary>
        public Span<BaseController> GetController => _controllers.AsSpan().Slice(0, _count);

        #endregion

        #region コンストラクタ

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="maxCapacity">最大容量（デフォルト: 100）</param>
        /// <param name="allocator">メモリアロケータ（デフォルト: Persistent）</param>
        public SoACharaDataDic(int maxCapacity = DEFAULT_MAX_CAPACITY, Allocator allocator = Allocator.Persistent)
        {
            _maxCapacity = maxCapacity;
            _allocator = allocator;
            _count = 0;
            _isDisposed = false;

            // バケット配列の初期化
            _buckets = new int[BUCKET_COUNT];
            _buckets.AsSpan().Fill(-1);

            // エントリリストの初期化
            _entries = new UnsafeList<Entry>(BUCKET_COUNT * 2, allocator);

            // 削除エントリ保管用のエントリリストも作成。
            _freeEntry = new Stack<int>(maxCapacity);

            // ハッシュ→データインデックスのマッピング
            _dataIndexToHash = new int[maxCapacity];
            _dataIndexToHash.AsSpan().Fill(-1);

            // メモリレイアウトの計算
            MemoryLayout layout = CalculateMemoryLayout(maxCapacity);
            _totalMemorySize = layout.TotalSize;

            // 一括メモリ確保
            _bulkMemory = (byte*)UnsafeUtility.Malloc(_totalMemorySize, 64, allocator);
            UnsafeUtility.MemClear(_bulkMemory, _totalMemorySize);

            // 各UnsafeListの初期化（固定サイズ）
            _characterBaseInfo = new UnsafeList<CharacterBaseInfo>(
                (CharacterBaseInfo*)(_bulkMemory + layout.BaseInfoOffset),
                maxCapacity
            );

            _characterAtkStatus = new UnsafeList<CharacterAtkStatus>(
                (CharacterAtkStatus*)(_bulkMemory + layout.AtkStatusOffset),
                maxCapacity
            );

            _characterDefStatus = new UnsafeList<CharacterDefStatus>(
                (CharacterDefStatus*)(_bulkMemory + layout.DefStatusOffset),
                maxCapacity
            );

            _solidData = new UnsafeList<SolidData>(
                (SolidData*)(_bulkMemory + layout.SolidDataOffset),
                maxCapacity
            );

            _characterStateInfo = new UnsafeList<CharacterStateInfo>(
                (CharacterStateInfo*)(_bulkMemory + layout.StateInfoOffset),
                maxCapacity
            );

            _moveStatus = new UnsafeList<MoveStatus>(
                (MoveStatus*)(_bulkMemory + layout.MoveStatusOffset),
                maxCapacity
            );

            _coldLog = new UnsafeList<CharaColdLog>(
                (CharaColdLog*)(_bulkMemory + layout.ColdLogOffset),
                maxCapacity
            );

            // ヘイト管理用配列の初期化
            _pHate = new NativeArray<PersonalHateContainer>(maxCapacity, Allocator.Persistent);

            // BaseController配列
            _controllers = new BaseController[maxCapacity];
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

            return AddByHash(obj.GetHashCode(), baseInfo, atkStatus, defStatus, solidData,
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
            if ( TryGetIndexByHash(hashCode, out int existingIndex) )
            {
                // 更新
                _characterBaseInfo[existingIndex] = baseInfo;
                _characterAtkStatus[existingIndex] = atkStatus;
                _characterDefStatus[existingIndex] = defStatus;
                _solidData[existingIndex] = solidData;
                _characterStateInfo[existingIndex] = stateInfo;
                _moveStatus[existingIndex] = moveStatus;
                _coldLog[existingIndex] = coldLog;
                _controllers[existingIndex] = controller;
                return existingIndex;
            }

            // 容量チェック
            if ( _count >= _maxCapacity )
            {
                throw new InvalidOperationException($"Maximum capacity ({_maxCapacity}) exceeded");
            }

            // 新規追加
            int dataIndex = _count;

            // データを追加
            _characterBaseInfo.AddNoResize(baseInfo);
            _characterAtkStatus.AddNoResize(atkStatus);
            _characterDefStatus.AddNoResize(defStatus);
            _solidData.AddNoResize(solidData);
            _characterStateInfo.AddNoResize(stateInfo);
            _moveStatus.AddNoResize(moveStatus);
            _coldLog.AddNoResize(coldLog);
            _controllers[dataIndex] = controller;

            // ハッシュテーブルへの登録
            int bucketIndex = GetBucketIndex(hashCode);

            // エントリの追加
            int newEntryIndex;

            // 新しいエントリはバケットの直下に追加される。
            // だから前の直下の要素を NextInBucket に繋げてる。
            var newEntry = new Entry
            {
                HashCode = hashCode,
                ValueIndex = dataIndex,
                NextInBucket = _buckets[bucketIndex]
            };

            // フリーリストから再利用 or 新規追加
            if ( _freeEntry.TryPop(out newEntryIndex) )
            {
                _entries[newEntryIndex] = newEntry;
            }
            // 再利用できない場合は最後尾にエントリ追加
            else
            {
                newEntryIndex = _entries.Length;
                _entries.AddNoResize(newEntry);
            }

            // バケットの直下に新エントリを追加
            _buckets[bucketIndex] = newEntryIndex;

            // マッピングの更新
            _dataIndexToHash[dataIndex] = hashCode;

            _count++;

            // 個人ヘイトも初期化。
            _pHate[dataIndex] = new PersonalHateContainer(5);

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
                return false;
            return RemoveByHash(obj.GetHashCode());
        }

        /// <summary>
        /// ハッシュコードに関連付けられたデータを削除（スワップ削除）
        /// O(1)の削除を実現するため、削除対象を最後の要素と入れ替える
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool RemoveByHash(int hashCode)
        {
            if ( !TryGetIndexByHash(hashCode, out int dataIndex) )
            {
                return false;
            }

            int lastIndex = _count - 1;

            // さっきまで使ってた個人ヘイトを削除。
            _pHate[dataIndex].personalHate.Dispose();

            // 削除対象が最後の要素でない場合は入れ替え
            if ( dataIndex != lastIndex )
            {
                // 最後の要素を削除位置にコピー
                _characterBaseInfo[dataIndex] = _characterBaseInfo[lastIndex];
                _characterAtkStatus[dataIndex] = _characterAtkStatus[lastIndex];
                _characterDefStatus[dataIndex] = _characterDefStatus[lastIndex];
                _solidData[dataIndex] = _solidData[lastIndex];
                _characterStateInfo[dataIndex] = _characterStateInfo[lastIndex];
                _moveStatus[dataIndex] = _moveStatus[lastIndex];
                _coldLog[dataIndex] = _coldLog[lastIndex];
                _controllers[dataIndex] = _controllers[lastIndex];

                // 個人ヘイトも更新
                _pHate[dataIndex] = _pHate[lastIndex];

                // 移動した要素のハッシュコードを見つけてマッピングを更新
                _dataIndexToHash[dataIndex] = _dataIndexToHash[lastIndex];

                // エントリ内の値インデックスも更新
                UpdateEntryDataIndex(_dataIndexToHash[lastIndex], dataIndex);

            }

            // リストの長さを減らす
            _characterBaseInfo.Length--;
            _characterAtkStatus.Length--;
            _characterDefStatus.Length--;
            _solidData.Length--;
            _characterStateInfo.Length--;
            _moveStatus.Length--;
            _coldLog.Length--;

            // ハッシュテーブルからエントリを削除
            RemoveFromHashTable(hashCode);

            _count--;
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
            int bucketIndex = GetBucketIndex(hashCode);

            // 現在のエントリの開始位置を取得する。
            int entryIndex = _buckets[bucketIndex];

            // ref参照でエントリを探し、エントリ内の値のインデックスを書き換える。
            while ( entryIndex != -1 )
            {
                ref Entry entry = ref _entries.ElementAt(entryIndex);

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
            int bucketIndex = GetBucketIndex(hashCode);

            // 削除対象のエントリの開始位置を取得する。
            int entryIndex = _buckets[bucketIndex];

            // 前のエントリが－１の時はバケットの直下のエントリ。
            int prevIndex = -1;

            while ( entryIndex != -1 )
            {
                ref Entry entry = ref _entries.ElementAt(entryIndex);

                // ハッシュ値が一致するエントリがあれば値インデックスを書き換える
                if ( entry.HashCode == hashCode )
                {
                    // バケット内の一つ目のエントリが削除対象なら、バケットからの参照を直接書き変える。
                    // [削除対象、次エントリ、次々エントリ] というバケットを[次エントリ、次々エントリ]にする 
                    if ( prevIndex == -1 )
                    {
                        _buckets[bucketIndex] = entry.NextInBucket;
                    }

                    // バケット内で前のエントリから参照されているなら、前のエントリに自分の次のエントリを繋ぎ直す。
                    // [前エントリ、削除対象、次エントリ] というバケットを[前エントリ、次エントリ]にする 
                    else
                    {
                        ref Entry prevEntry = ref _entries.ElementAt(prevIndex);
                        prevEntry.NextInBucket = entry.NextInBucket;
                    }

                    // 解放されたインデックスをスタックに追加
                    _freeEntry.Push(entryIndex);
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

            return TryGetValueByHash(obj.GetHashCode(), out baseInfo, out atkStatus, out defStatus, out solidData,
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
            if ( TryGetIndexByHash(hashCode, out int dataIndex) )
            {
                baseInfo = _characterBaseInfo[dataIndex];
                atkStatus = _characterAtkStatus[dataIndex];
                defStatus = _characterDefStatus[dataIndex];
                solidData = _solidData[dataIndex];
                stateInfo = _characterStateInfo[dataIndex];
                moveStatus = _moveStatus[dataIndex];
                coldLog = _coldLog[dataIndex];
                controller = _controllers[dataIndex];
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
            if ( index < 0 || index >= _count )
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

            baseInfo = _characterBaseInfo[index];
            atkStatus = _characterAtkStatus[index];
            defStatus = _characterDefStatus[index];
            solidData = _solidData[index];
            stateInfo = _characterStateInfo[index];
            moveStatus = _moveStatus[index];
            coldLog = _coldLog[index];
            controller = _controllers[index];
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
            if ( index < 0 || index >= _count )
                throw new ArgumentOutOfRangeException(nameof(index));

            return ref _characterBaseInfo.ElementAt(index);
        }

        /// <summary>
        /// インデックスからCharacterAtkStatusを取得。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref CharacterAtkStatus GetCharacterAtkStatusByIndex(int index)
        {
            if ( index < 0 || index >= _count )
                throw new ArgumentOutOfRangeException(nameof(index));

            return ref _characterAtkStatus.ElementAt(index);
        }

        /// <summary>
        /// インデックスからCharacterDefStatusを取得。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref CharacterDefStatus GetCharacterDefStatusByIndex(int index)
        {
            if ( index < 0 || index >= _count )
                throw new ArgumentOutOfRangeException(nameof(index));

            return ref _characterDefStatus.ElementAt(index);
        }

        /// <summary>
        /// インデックスからCharacterStateInfoを取得。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref SolidData GetSolidDataByIndex(int index)
        {
            if ( index < 0 || index >= _count )
                throw new ArgumentOutOfRangeException(nameof(index));

            return ref _solidData.ElementAt(index);
        }

        /// <summary>
        /// インデックスからCharacterStateInfoを取得。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref CharacterStateInfo GetCharacterStateInfoByIndex(int index)
        {
            if ( index < 0 || index >= _count )
                throw new ArgumentOutOfRangeException(nameof(index));

            return ref _characterStateInfo.ElementAt(index);
        }

        /// <summary>
        /// インデックスからMoveStatusを取得。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref MoveStatus GetMoveStatusByIndex(int index)
        {
            if ( index < 0 || index >= _count )
                throw new ArgumentOutOfRangeException(nameof(index));

            return ref _moveStatus.ElementAt(index);
        }

        /// <summary>
        /// インデックスからCharaColdLogを取得。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref CharaColdLog GetCharaColdLogByIndex(int index)
        {
            if ( index < 0 || index >= _count )
                throw new ArgumentOutOfRangeException(nameof(index));

            return ref _coldLog.ElementAt(index);
        }

        #endregion

        #region ユーティリティ

        /// <summary>
        /// すべてのエントリをクリア
        /// </summary>
        public void Clear()
        {
            // バケットをリセット
            _buckets.AsSpan().Fill(-1);

            // エントリとマッピングをクリア
            _entries.Clear();
            _dataIndexToHash.AsSpan().Fill(-1);

            // データをクリア（Lengthを0にするだけ）
            _characterBaseInfo.Length = 0;
            _characterAtkStatus.Length = 0;
            _characterDefStatus.Length = 0;
            _solidData.Length = 0;
            _characterStateInfo.Length = 0;
            _moveStatus.Length = 0;
            _coldLog.Length = 0;

            // コントローラー配列をクリア
            Array.Clear(_controllers, 0, _count);

            _count = 0;
        }

        /// <summary>
        /// 指定したキーが存在するか確認
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ContainsKey(GameObject obj)
        {
            if ( obj == null )
                return false;
            return ContainsKeyByHash(obj.GetHashCode());
        }

        /// <summary>
        /// 指定したハッシュコードが存在するか確認
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ContainsKeyByHash(int hashCode)
        {
            return TryGetIndexByHash(hashCode, out int _);
        }

        /// <summary>
        /// すべての有効なエントリに対して処理を実行
        /// </summary>
        public void ForEach(Action<int, CharacterBaseInfo, CharacterAtkStatus, CharacterDefStatus, SolidData,
                                  CharacterStateInfo, MoveStatus, CharaColdLog, BaseController> action)
        {
            if ( action == null )
                throw new ArgumentNullException(nameof(action));

            for ( int i = 0; i < _count; i++ )
            {
                action(i,
                      _characterBaseInfo[i],
                      _characterAtkStatus[i],
                      _characterDefStatus[i],
                      _solidData[i],
                      _characterStateInfo[i],
                      _moveStatus[i],
                      _coldLog[i],
                      _controllers[i]);
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
            var layout = new MemoryLayout();
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

        #region IDisposable

        /// <summary>
        /// リソースの解放
        /// </summary>
        public void Dispose()
        {
            if ( _isDisposed )
                return;

            if ( _bulkMemory != null )
            {
                UnsafeUtility.Free(_bulkMemory, _allocator);
                _bulkMemory = null;
            }

            _entries.Dispose();

            _isDisposed = true;
        }

        #endregion
    }
}