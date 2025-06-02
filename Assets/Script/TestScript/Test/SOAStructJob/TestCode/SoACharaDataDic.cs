using CharacterController;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using static TestScript.SOATest.SOAStatus;

namespace TestScript.Collections
{
    /// <summary>
    /// キャラクターデータの種類を表す列挙型
    /// </summary>
    public enum CharacterDataType
    {
        BaseInfo,
        AtkStatus,
        DefStatus,
        StateInfo,
        MoveStatus,
        ColdLog,
        CharacterController
    }

    /// <summary>
    /// ゲームオブジェクトのGetHashCode()をキーとして管理するキャラクターデータ辞書
    /// UnityではGetHashCode()がGetInstanceID()と同じ値を返すため、キーの一意性が保証されている。
    /// 内部でのデータ保持にNativeListを使用してGC負荷を削減
    /// SoAパターンでキャラクターの各種データを効率的に管理
    /// </summary>
    public class SoACharaDataDic : IDisposable
    {
        /// <summary>
        /// エントリ構造体 - キーと値とチェーン情報を格納<br></br>
        /// ハッシュコード→バケットID→エントリ→実際のインデックスという流れ。
        /// </summary>
        private struct Entry
        {
            /// <summary>
            /// ゲームオブジェクトのハッシュコード（GetInstanceID()と同一）
            /// </summary>
            public int HashCode;

            /// <summary>
            /// 値配列内のインデックス
            /// </summary>
            public int ValueIndex;

            /// <summary>
            /// 同じバケット内の次のエントリへのインデックス。
            /// </summary>
            public int NextInBucket;

            /// <summary>
            /// このエントリが使用中かどうか
            /// </summary>
            public bool IsOccupied;
        }

        /// <summary>
        /// バケット配列（各要素はエントリへのインデックス、-1は空）
        /// </summary>
        private NativeList<int> _buckets;

        /// <summary>
        /// エントリのリスト
        /// </summary>
        private NativeList<Entry> _entries;

        #region 管理対象のデータ。SoAに従いキャラの数だけ持つ。

        /// <summary>
        /// キャラクターの基本情報（HP、MP、位置）
        /// </summary>
        private NativeList<CharacterBaseInfo> _characterBaseInfo;

        /// <summary>
        /// 攻撃力のデータ
        /// </summary>
        private NativeList<CharacterAtkStatus> _characterAtkStatus;

        /// <summary>
        /// 防御力のデータ
        /// </summary>
        private NativeList<CharacterDefStatus> _characterDefStatus;

        /// <summary>
        /// AIが参照するためのキャラクターの状態情報
        /// </summary>
        private NativeList<CharacterStateInfo> _characterStateInfo;

        /// <summary>
        /// キャラの行動（歩行速度とか）のステータス。
        /// </summary>
        private NativeList<MoveStatus> _moveStatus;

        /// <summary>
        /// 参照頻度が少なく、加えて連続参照されないデータを集めた構造体。
        /// </summary>
        private NativeList<CharaColdLog> _coldLog;

        /// <summary>
        /// BaseControllerを格納する配列（managed型のため通常の配列）
        /// </summary>
        private BaseController[] _controllers;

        #endregion

        /// <summary>
        /// 使用中のエントリ数
        /// </summary>
        private int _count;

        /// <summary>
        /// 削除済みエントリの再利用リスト先頭
        /// </summary>
        private int _freeListHead;

        /// <summary>
        /// 再利用可能なエントリ数
        /// </summary>
        private int _freeCount;

        /// <summary>
        /// 解放済みフラグ
        /// </summary>
        private bool _isDisposed;

        /// <summary>
        /// メモリの確保のタイプ
        /// </summary>
        private readonly Allocator _allocator;

        /// <summary>
        /// 素数テーブル
        /// リサイズの際に使用する。
        /// </summary>
        private static readonly int[] PrimeSizes = {
            17, 37, 79, 163, 331, 673, 1361, 2729, 5471, 10949, 21911, 43853,
            87719, 175447, 350899, 701819, 1403641, 2807303, 5614657, 11229331
        };

        /// <summary>
        /// 初期サイズ定数
        /// </summary>
        private const int DEFAULT_CAPACITY = 1031;

        /// <summary>
        /// 負荷係数（収容済み要素の割合がこの値を超えるとリサイズ）
        /// </summary>
        private const float LOAD_FACTOR = 0.75f;

        /// <summary>
        /// 格納されている要素数
        /// </summary>
        public int Count => this._count - this._freeCount;

        /// <summary>
        /// バケットの容量
        /// </summary>
        public int Capacity => this._entries.Length;

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="capacity">初期容量（素数に調整されます）</param>
        /// <param name="allocator">メモリアロケータ（デフォルトはPersistent）</param>
        public SoACharaDataDic(int capacity = DEFAULT_CAPACITY, Allocator allocator = Allocator.Persistent)
        {
            // アロケータ保存
            this._allocator = allocator;

            // 指定容量以上の最小の素数を選択
            int primeCapacity = this.GetNextPrimeSize(capacity);

            // NativeListの初期化
            this._buckets = new NativeList<int>(primeCapacity, allocator);
            this._entries = new NativeList<Entry>(primeCapacity, allocator);

            this._characterBaseInfo = new NativeList<CharacterBaseInfo>(primeCapacity, allocator);
            this._characterBaseInfo.Capacity = primeCapacity;
            this._characterAtkStatus = new NativeList<CharacterAtkStatus>(primeCapacity, allocator);
            this._characterAtkStatus.Capacity = primeCapacity;
            this._characterDefStatus = new NativeList<CharacterDefStatus>(primeCapacity, allocator);
            this._characterDefStatus.Capacity = primeCapacity;
            this._characterStateInfo = new NativeList<CharacterStateInfo>(primeCapacity, allocator);
            this._characterStateInfo.Capacity = primeCapacity;
            this._moveStatus = new NativeList<MoveStatus>(primeCapacity, allocator);
            this._moveStatus.Capacity = primeCapacity;
            this._coldLog = new NativeList<CharaColdLog>(primeCapacity, allocator);
            this._coldLog.Capacity = primeCapacity;
            this._controllers = new BaseController[primeCapacity];

            // バケットリストの容量確保と-1で初期化
            this._buckets.Resize(primeCapacity, NativeArrayOptions.ClearMemory);
            for ( int i = 0; i < this._buckets.Length; i++ )
            {
                this._buckets[i] = -1;
            }

            // エントリリストの容量を確保
            this._entries.Capacity = primeCapacity;

            this._freeListHead = -1;
            this._count = 0;
            this._freeCount = 0;
        }

        /// <summary>
        /// ゲームオブジェクトと全キャラクターデータを追加または更新
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Add(GameObject obj, CharacterBaseInfo baseInfo, CharacterAtkStatus atkStatus,
                      CharacterDefStatus defStatus, CharacterStateInfo stateInfo,
                      MoveStatus moveStatus, CharaColdLog coldLog, BaseController controller)
        {
            if ( obj == null )
            {
                throw new ArgumentNullException(nameof(obj));
            }

            return this.AddByHash(obj.GetHashCode(), baseInfo, atkStatus, defStatus,
                                 stateInfo, moveStatus, coldLog, controller);
        }

        /// <summary>
        /// ハッシュコードと全キャラクターデータを追加または更新し、値のインデックスを返す
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int AddByHash(int hashCode, CharacterBaseInfo baseInfo, CharacterAtkStatus atkStatus,
                            CharacterDefStatus defStatus, CharacterStateInfo stateInfo,
                            MoveStatus moveStatus, CharaColdLog coldLog, BaseController controller)
        {
            // 負荷係数チェック - 必要に応じてリサイズ
            if ( (this._count - this._freeCount) >= this._entries.Length * LOAD_FACTOR )
            {
                this.Resize(this._entries.Length * 2);
            }

            // バケットインデックスを計算（単純モジュロ法）
            int bucketIndex = this.GetBucketIndex(hashCode);

            // 同じキーが既に存在するか検索
            int entryIndex = this._buckets[bucketIndex];
            while ( entryIndex != -1 )
            {
                ref Entry entry = ref this._entries.ElementAt(entryIndex);
                if ( entry.HashCode == hashCode )
                {
                    // 既存エントリを更新
                    this._characterBaseInfo[entry.ValueIndex] = baseInfo;
                    this._characterAtkStatus[entry.ValueIndex] = atkStatus;
                    this._characterDefStatus[entry.ValueIndex] = defStatus;
                    this._characterStateInfo[entry.ValueIndex] = stateInfo;
                    this._moveStatus[entry.ValueIndex] = moveStatus;
                    this._coldLog[entry.ValueIndex] = coldLog;
                    this._controllers[entry.ValueIndex] = controller;
                    return entry.ValueIndex;
                }

                entryIndex = entry.NextInBucket;
            }

            // 新しいエントリ用のインデックスを確保
            int newIndex;
            if ( this._freeCount > 0 )
            {
                // 削除済みエントリを再利用
                newIndex = this._freeListHead;
                this._freeListHead = this._entries.ElementAt(newIndex).NextInBucket;
                this._freeCount--;
            }
            else
            {
                // 新しいスロットを使用
                if ( this._count == this._entries.Length )
                {
                    this.Resize(this._entries.Length * 2);
                    bucketIndex = this.GetBucketIndex(hashCode);
                }

                newIndex = this._count;
                this._count++;
            }

            // エントリと値のリストが十分な大きさになるよう拡張
            this.EnsureCapacity(newIndex);

            // 新しいエントリの設定
            Entry newEntry;
            newEntry.HashCode = hashCode;
            newEntry.ValueIndex = newIndex;
            newEntry.NextInBucket = this._buckets[bucketIndex];
            newEntry.IsOccupied = true;

            // NativeListとコントローラー配列の更新
            if ( newIndex < this._entries.Length )
            {
                this._entries[newIndex] = newEntry;
            }
            else
            {
                this._entries.Add(newEntry);
            }

            // 各データリストにデータを追加
            if ( newIndex < this._characterBaseInfo.Length )
            {
                this._characterBaseInfo[newIndex] = baseInfo;
                this._characterAtkStatus[newIndex] = atkStatus;
                this._characterDefStatus[newIndex] = defStatus;
                this._characterStateInfo[newIndex] = stateInfo;
                this._moveStatus[newIndex] = moveStatus;
                this._coldLog[newIndex] = coldLog;
            }
            else
            {
                this._characterBaseInfo.Add(baseInfo);
                this._characterAtkStatus.Add(atkStatus);
                this._characterDefStatus.Add(defStatus);
                this._characterStateInfo.Add(stateInfo);
                this._moveStatus.Add(moveStatus);
                this._coldLog.Add(coldLog);
            }

            // コントローラーはmanaged型の配列なので単純に代入
            this._controllers[newIndex] = controller;

            this._buckets[bucketIndex] = newIndex;

            return newIndex;
        }

        /// <summary>
        /// 特定のインデックスに対応するリストの容量を確保
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void EnsureCapacity(int index)
        {
            // 必要に応じて各リストの容量を拡張
            if ( this._entries.Capacity <= index )
            {
                int newCapacity = Math.Max(this._entries.Capacity * 2, index + 1);
                this._entries.Capacity = newCapacity;
            }

            if ( this._characterBaseInfo.Capacity <= index )
            {
                int newCapacity = Math.Max(this._characterBaseInfo.Capacity * 2, index + 1);
                this._characterBaseInfo.Capacity = newCapacity;
                this._characterAtkStatus.Capacity = newCapacity;
                this._characterDefStatus.Capacity = newCapacity;
                this._characterStateInfo.Capacity = newCapacity;
                this._moveStatus.Capacity = newCapacity;
                this._coldLog.Capacity = newCapacity;
            }

            if ( this._controllers.Length <= index )
            {
                int newCapacity = Math.Max(this._controllers.Length * 2, index + 1);
                BaseController[] newArray = new BaseController[newCapacity];
                Array.Copy(this._controllers, newArray, this._controllers.Length);
                this._controllers = newArray;
            }
        }

        /// <summary>
        /// 指定したインデックスが有効（使用中）かどうかを確認
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsValidIndex(int index)
        {
            return index >= 0 && index < this._count && this._entries[index].IsOccupied;
        }

        /// <summary>
        /// インデックスから直接指定されたデータタイプのデータを取得
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T GetDataByIndex<T>(int index, CharacterDataType dataType) where T : struct
        {
            if ( !this.IsValidIndex(index) )
            {
                throw new ArgumentOutOfRangeException(nameof(index), "Index is out of range or points to a removed entry");
            }

            return dataType switch
            {
                CharacterDataType.BaseInfo => (T)(object)this._characterBaseInfo[index],
                CharacterDataType.AtkStatus => (T)(object)this._characterAtkStatus[index],
                CharacterDataType.DefStatus => (T)(object)this._characterDefStatus[index],
                CharacterDataType.StateInfo => (T)(object)this._characterStateInfo[index],
                CharacterDataType.MoveStatus => (T)(object)this._moveStatus[index],
                CharacterDataType.ColdLog => (T)(object)this._coldLog[index],
                _ => throw new ArgumentException($"Invalid data type: {dataType}")
            };
        }

        /// <summary>
        /// インデックスから直接BaseControllerを取得
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public BaseController GetControllerByIndex(int index)
        {
            if ( !this.IsValidIndex(index) )
            {
                throw new ArgumentOutOfRangeException(nameof(index), "Index is out of range or points to a removed entry");
            }

            return this._controllers[index];
        }

        /// <summary>
        /// インデックスから直接BaseControllerを設定
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetControllerByIndex(int index, BaseController controller)
        {
            if ( !this.IsValidIndex(index) )
            {
                throw new ArgumentOutOfRangeException(nameof(index), "Index is out of range or points to a removed entry");
            }

            this._controllers[index] = controller;
        }

        /// <summary>
        /// ゲームオブジェクトから全キャラクターデータと内部インデックスを取得
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetValue(GameObject obj, out CharacterBaseInfo baseInfo, out CharacterAtkStatus atkStatus,
                               out CharacterDefStatus defStatus, out CharacterStateInfo stateInfo,
                               out MoveStatus moveStatus, out CharaColdLog coldLog,
                               out BaseController controller, out int index)
        {
            if ( obj == null )
            {
                baseInfo = default;
                atkStatus = default;
                defStatus = default;
                stateInfo = default;
                moveStatus = default;
                coldLog = default;
                controller = null;
                index = -1;
                return false;
            }

            return this.TryGetValueByHash(obj.GetHashCode(), out baseInfo, out atkStatus, out defStatus,
                                         out stateInfo, out moveStatus, out coldLog, out controller, out index);
        }

        /// <summary>
        /// ハッシュコードから全キャラクターデータと内部インデックスを取得
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetValueByHash(int hashCode, out CharacterBaseInfo baseInfo, out CharacterAtkStatus atkStatus,
                                     out CharacterDefStatus defStatus, out CharacterStateInfo stateInfo,
                                     out MoveStatus moveStatus, out CharaColdLog coldLog,
                                     out BaseController controller, out int index)
        {
            int bucketIndex = this.GetBucketIndex(hashCode);
            int entryIndex = this._buckets[bucketIndex];

            while ( entryIndex != -1 )
            {
                ref Entry entry = ref this._entries.ElementAt(entryIndex);
                if ( entry.HashCode == hashCode )
                {
                    baseInfo = this._characterBaseInfo[entry.ValueIndex];
                    atkStatus = this._characterAtkStatus[entry.ValueIndex];
                    defStatus = this._characterDefStatus[entry.ValueIndex];
                    stateInfo = this._characterStateInfo[entry.ValueIndex];
                    moveStatus = this._moveStatus[entry.ValueIndex];
                    coldLog = this._coldLog[entry.ValueIndex];
                    controller = this._controllers[entry.ValueIndex];
                    index = entry.ValueIndex;
                    return true;
                }

                entryIndex = entry.NextInBucket;
            }

            baseInfo = default;
            atkStatus = default;
            defStatus = default;
            stateInfo = default;
            moveStatus = default;
            coldLog = default;
            controller = null;
            index = -1;
            return false;
        }

        /// <summary>
        /// ゲームオブジェクトに関連付けられたデータを削除
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
        /// ハッシュコードに関連付けられたデータを削除
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool RemoveByHash(int hashCode)
        {
            if ( this._count == 0 )
            {
                return false;
            }

            int bucketIndex = this.GetBucketIndex(hashCode);
            int entryIndex = this._buckets[bucketIndex];
            int prevIndex = -1;

            while ( entryIndex != -1 )
            {
                ref Entry entry = ref this._entries.ElementAt(entryIndex);

                if ( entry.HashCode == hashCode )
                {
                    // 各データを論理削除（ILogicalDelateインターフェースを実装している場合）
                    // this._characterBaseInfo[entry.ValueIndex].LogicalDelete();
                    // 他のデータもリセット
                    this._characterBaseInfo[entry.ValueIndex] = default;
                    this._characterAtkStatus[entry.ValueIndex] = default;
                    this._characterDefStatus[entry.ValueIndex] = default;
                    this._characterStateInfo[entry.ValueIndex] = default;
                    this._moveStatus[entry.ValueIndex] = default;
                    this._coldLog[entry.ValueIndex] = default;
                    this._controllers[entry.ValueIndex] = null;

                    // エントリをバケットリストから削除
                    if ( prevIndex != -1 )
                    {
                        Entry prevEntry = this._entries[prevIndex];
                        prevEntry.NextInBucket = entry.NextInBucket;
                        this._entries[prevIndex] = prevEntry;
                    }
                    else
                    {
                        this._buckets[bucketIndex] = entry.NextInBucket;
                    }

                    // エントリを論理的に削除してフリーリストに追加
                    Entry updatedEntry = entry;
                    updatedEntry.IsOccupied = false;
                    updatedEntry.NextInBucket = this._freeListHead;
                    this._entries[entryIndex] = updatedEntry;

                    this._freeListHead = entryIndex;
                    this._freeCount++;

                    return true;
                }

                prevIndex = entryIndex;
                entryIndex = entry.NextInBucket;
            }

            return false;
        }

        /// <summary>
        /// すべてのエントリをクリア
        /// </summary>
        public void Clear()
        {
            if ( this._count == 0 )
            {
                return;
            }

            // バケットを-1で初期化
            for ( int i = 0; i < this._buckets.Length; i++ )
            {
                this._buckets[i] = -1;
            }

            // NativeListをクリア
            this._entries.Clear();
            this._characterBaseInfo.Clear();
            this._characterAtkStatus.Clear();
            this._characterDefStatus.Clear();
            this._characterStateInfo.Clear();
            this._moveStatus.Clear();
            this._coldLog.Clear();

            // コントローラー配列をクリア
            Array.Clear(this._controllers, 0, this._controllers.Length);

            this._count = 0;
            this._freeCount = 0;
            this._freeListHead = -1;
        }

        #region 内部データ取得プロパティ

        #region 内部データ取得プロパティ - CharacterBaseInfo

        /// <summary>
        /// GameObjectからCharacterBaseInfoを取得（参照を返す）
        /// 最も汎用的だが、ハッシュ計算とバケット検索が発生するため中程度の速度
        /// </summary>
        /// <param name="obj">対象のGameObject</param>
        /// <returns>CharacterBaseInfoの参照</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref CharacterBaseInfo GetCharacterBaseInfoByGameObject(GameObject obj)
        {
            if ( obj == null )
            {
                throw new ArgumentNullException(nameof(obj));
            }

            return ref this.GetCharacterBaseInfoByHash(obj.GetHashCode());
        }

        /// <summary>
        /// ハッシュ値からCharacterBaseInfoを取得（参照を返す）
        /// バケット検索が発生するため中程度の速度、GameObjectよりはやや高速
        /// </summary>
        /// <param name="hashCode">GameObjectのハッシュ値</param>
        /// <returns>CharacterBaseInfoの参照</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref CharacterBaseInfo GetCharacterBaseInfoByHash(int hashCode)
        {
            int bucketIndex = this.GetBucketIndex(hashCode);
            int entryIndex = this._buckets[bucketIndex];

            while ( entryIndex != -1 )
            {
                ref Entry entry = ref this._entries.ElementAt(entryIndex);
                if ( entry.HashCode == hashCode )
                {
                    return ref this._characterBaseInfo.ElementAt(entry.ValueIndex);
                }
                entryIndex = entry.NextInBucket;
            }

            throw new KeyNotFoundException($"HashCode {hashCode} not found in dictionary");
        }

        /// <summary>
        /// インデックスからCharacterBaseInfoを直接取得（参照を返す）
        /// 最高速度、配列への直接アクセスのため
        /// </summary>
        /// <param name="index">内部配列のインデックス</param>
        /// <returns>CharacterBaseInfoの参照</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref CharacterBaseInfo GetCharacterBaseInfoByIndex(int index)
        {
            if ( !this.IsValidIndex(index) )
            {
                throw new ArgumentOutOfRangeException(nameof(index), "Index is out of range or points to a removed entry");
            }

            return ref this._characterBaseInfo.ElementAt(index);
        }

        #endregion

        #region 内部データ取得プロパティ - CharacterAtkStatus

        /// <summary>
        /// GameObjectからCharacterAtkStatusを取得（参照を返す）
        /// 攻撃力データへの最も汎用的なアクセス方法
        /// </summary>
        /// <param name="obj">対象のGameObject</param>
        /// <returns>CharacterAtkStatusの参照</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref CharacterAtkStatus GetCharacterAtkStatusByGameObject(GameObject obj)
        {
            if ( obj == null )
            {
                throw new ArgumentNullException(nameof(obj));
            }

            return ref this.GetCharacterAtkStatusByHash(obj.GetHashCode());
        }

        /// <summary>
        /// ハッシュ値からCharacterAtkStatusを取得（参照を返す）
        /// 攻撃力データへの中程度速度でのアクセス
        /// </summary>
        /// <param name="hashCode">GameObjectのハッシュ値</param>
        /// <returns>CharacterAtkStatusの参照</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref CharacterAtkStatus GetCharacterAtkStatusByHash(int hashCode)
        {
            int bucketIndex = this.GetBucketIndex(hashCode);
            int entryIndex = this._buckets[bucketIndex];

            while ( entryIndex != -1 )
            {
                ref Entry entry = ref this._entries.ElementAt(entryIndex);
                if ( entry.HashCode == hashCode )
                {
                    return ref this._characterAtkStatus.ElementAt(entry.ValueIndex);
                }
                entryIndex = entry.NextInBucket;
            }

            throw new KeyNotFoundException($"HashCode {hashCode} not found in dictionary");
        }

        /// <summary>
        /// インデックスからCharacterAtkStatusを直接取得（参照を返す）
        /// 攻撃力データへの最高速度アクセス、Job内での使用に最適
        /// </summary>
        /// <param name="index">内部配列のインデックス</param>
        /// <returns>CharacterAtkStatusの参照</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref CharacterAtkStatus GetCharacterAtkStatusByIndex(int index)
        {
            if ( !this.IsValidIndex(index) )
            {
                throw new ArgumentOutOfRangeException(nameof(index), "Index is out of range or points to a removed entry");
            }

            return ref this._characterAtkStatus.ElementAt(index);
        }

        #endregion

        #region 内部データ取得プロパティ - CharacterDefStatus

        /// <summary>
        /// GameObjectからCharacterDefStatusを取得（参照を返す）
        /// 防御力データへの汎用的なアクセス、UIやスクリプトからの参照に適している
        /// </summary>
        /// <param name="obj">対象のGameObject</param>
        /// <returns>CharacterDefStatusの参照</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref CharacterDefStatus GetCharacterDefStatusByGameObject(GameObject obj)
        {
            if ( obj == null )
            {
                throw new ArgumentNullException(nameof(obj));
            }

            return ref this.GetCharacterDefStatusByHash(obj.GetHashCode());
        }

        /// <summary>
        /// ハッシュ値からCharacterDefStatusを取得（参照を返す）
        /// 防御力データへの効率的なアクセス方法
        /// </summary>
        /// <param name="hashCode">GameObjectのハッシュ値</param>
        /// <returns>CharacterDefStatusの参照</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref CharacterDefStatus GetCharacterDefStatusByHash(int hashCode)
        {
            int bucketIndex = this.GetBucketIndex(hashCode);
            int entryIndex = this._buckets[bucketIndex];

            while ( entryIndex != -1 )
            {
                ref Entry entry = ref this._entries.ElementAt(entryIndex);
                if ( entry.HashCode == hashCode )
                {
                    return ref this._characterDefStatus.ElementAt(entry.ValueIndex);
                }
                entryIndex = entry.NextInBucket;
            }

            throw new KeyNotFoundException($"HashCode {hashCode} not found in dictionary");
        }

        /// <summary>
        /// インデックスからCharacterDefStatusを直接取得（参照を返す）
        /// 防御力データへの最高速度アクセス、ダメージ計算処理などで重要
        /// </summary>
        /// <param name="index">内部配列のインデックス</param>
        /// <returns>CharacterDefStatusの参照</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref CharacterDefStatus GetCharacterDefStatusByIndex(int index)
        {
            if ( !this.IsValidIndex(index) )
            {
                throw new ArgumentOutOfRangeException(nameof(index), "Index is out of range or points to a removed entry");
            }

            return ref this._characterDefStatus.ElementAt(index);
        }

        #endregion

        #region 内部データ取得プロパティ - CharacterStateInfo

        /// <summary>
        /// GameObjectからCharacterStateInfoを取得（参照を返す）
        /// AIの状態情報への汎用的なアクセス、デバッグやUI表示で使用
        /// </summary>
        /// <param name="obj">対象のGameObject</param>
        /// <returns>CharacterStateInfoの参照</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref CharacterStateInfo GetCharacterStateInfoByGameObject(GameObject obj)
        {
            if ( obj == null )
            {
                throw new ArgumentNullException(nameof(obj));
            }

            return ref this.GetCharacterStateInfoByHash(obj.GetHashCode());
        }

        /// <summary>
        /// ハッシュ値からCharacterStateInfoを取得（参照を返す）
        /// AI状態情報への効率的なアクセス、AIマネージャーから呼び出される
        /// </summary>
        /// <param name="hashCode">GameObjectのハッシュ値</param>
        /// <returns>CharacterStateInfoの参照</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref CharacterStateInfo GetCharacterStateInfoByHash(int hashCode)
        {
            int bucketIndex = this.GetBucketIndex(hashCode);
            int entryIndex = this._buckets[bucketIndex];

            while ( entryIndex != -1 )
            {
                ref Entry entry = ref this._entries.ElementAt(entryIndex);
                if ( entry.HashCode == hashCode )
                {
                    return ref this._characterStateInfo.ElementAt(entry.ValueIndex);
                }
                entryIndex = entry.NextInBucket;
            }

            throw new KeyNotFoundException($"HashCode {hashCode} not found in dictionary");
        }

        /// <summary>
        /// インデックスからCharacterStateInfoを直接取得（参照を返す）
        /// AI状態情報への最高速度アクセス、AI判断Jobでの使用に最適化
        /// </summary>
        /// <param name="index">内部配列のインデックス</param>
        /// <returns>CharacterStateInfoの参照</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref CharacterStateInfo GetCharacterStateInfoByIndex(int index)
        {
            if ( !this.IsValidIndex(index) )
            {
                throw new ArgumentOutOfRangeException(nameof(index), "Index is out of range or points to a removed entry");
            }

            return ref this._characterStateInfo.ElementAt(index);
        }

        #endregion

        #region 内部データ取得プロパティ - MoveStatus

        /// <summary>
        /// GameObjectからMoveStatusを取得（参照を返す）
        /// 移動関連ステータスへの汎用的なアクセス、移動速度の参照などで使用
        /// </summary>
        /// <param name="obj">対象のGameObject</param>
        /// <returns>MoveStatusの参照</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref MoveStatus GetMoveStatusByGameObject(GameObject obj)
        {
            if ( obj == null )
            {
                throw new ArgumentNullException(nameof(obj));
            }

            return ref this.GetMoveStatusByHash(obj.GetHashCode());
        }

        /// <summary>
        /// ハッシュ値からMoveStatusを取得（参照を返す）
        /// 移動ステータスへの効率的なアクセス方法
        /// </summary>
        /// <param name="hashCode">GameObjectのハッシュ値</param>
        /// <returns>MoveStatusの参照</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref MoveStatus GetMoveStatusByHash(int hashCode)
        {
            int bucketIndex = this.GetBucketIndex(hashCode);
            int entryIndex = this._buckets[bucketIndex];

            while ( entryIndex != -1 )
            {
                ref Entry entry = ref this._entries.ElementAt(entryIndex);
                if ( entry.HashCode == hashCode )
                {
                    return ref this._moveStatus.ElementAt(entry.ValueIndex);
                }
                entryIndex = entry.NextInBucket;
            }

            throw new KeyNotFoundException($"HashCode {hashCode} not found in dictionary");
        }

        /// <summary>
        /// インデックスからMoveStatusを直接取得（参照を返す）
        /// 移動ステータスへの最高速度アクセス、移動系Jobでのパフォーマンス重視
        /// </summary>
        /// <param name="index">内部配列のインデックス</param>
        /// <returns>MoveStatusの参照</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref MoveStatus GetMoveStatusByIndex(int index)
        {
            if ( !this.IsValidIndex(index) )
            {
                throw new ArgumentOutOfRangeException(nameof(index), "Index is out of range or points to a removed entry");
            }

            return ref this._moveStatus.ElementAt(index);
        }

        #endregion

        #region 内部データ取得プロパティ - CharaColdLog

        /// <summary>
        /// GameObjectからCharaColdLogを取得（参照を返す）
        /// 低頻度アクセスデータへの汎用的な取得方法、統計情報の参照などで使用
        /// </summary>
        /// <param name="obj">対象のGameObject</param>
        /// <returns>CharaColdLogの参照</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref CharaColdLog GetCharaColdLogByGameObject(GameObject obj)
        {
            if ( obj == null )
            {
                throw new ArgumentNullException(nameof(obj));
            }

            return ref this.GetCharaColdLogByHash(obj.GetHashCode());
        }

        /// <summary>
        /// ハッシュ値からCharaColdLogを取得（参照を返す）
        /// 低頻度データへの効率的なアクセス、バックグラウンド処理で使用
        /// </summary>
        /// <param name="hashCode">GameObjectのハッシュ値</param>
        /// <returns>CharaColdLogの参照</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref CharaColdLog GetCharaColdLogByHash(int hashCode)
        {
            int bucketIndex = this.GetBucketIndex(hashCode);
            int entryIndex = this._buckets[bucketIndex];

            while ( entryIndex != -1 )
            {
                ref Entry entry = ref this._entries.ElementAt(entryIndex);
                if ( entry.HashCode == hashCode )
                {
                    return ref this._coldLog.ElementAt(entry.ValueIndex);
                }
                entryIndex = entry.NextInBucket;
            }

            throw new KeyNotFoundException($"HashCode {hashCode} not found in dictionary");
        }

        /// <summary>
        /// インデックスからCharaColdLogを直接取得（参照を返す）
        /// 低頻度データへの直接アクセス、メンテナンスJobなどで使用
        /// </summary>
        /// <param name="index">内部配列のインデックス</param>
        /// <returns>CharaColdLogの参照</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref CharaColdLog GetCharaColdLogByIndex(int index)
        {
            if ( !this.IsValidIndex(index) )
            {
                throw new ArgumentOutOfRangeException(nameof(index), "Index is out of range or points to a removed entry");
            }

            return ref this._coldLog.ElementAt(index);
        }

        #endregion

        #region 内部データ取得プロパティ - BaseController

        /// <summary>
        /// GameObjectからBaseControllerを取得（値を返す）
        /// キャラクターコントローラーへの汎用的なアクセス、UI更新などで使用
        /// managed型のため参照ではなく値を返す
        /// </summary>
        /// <param name="obj">対象のGameObject</param>
        /// <returns>BaseControllerのインスタンス</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public BaseController GetBaseControllerByGameObject(GameObject obj)
        {
            if ( obj == null )
            {
                throw new ArgumentNullException(nameof(obj));
            }

            return this.GetBaseControllerByHash(obj.GetHashCode());
        }

        /// <summary>
        /// ハッシュ値からBaseControllerを取得（値を返す）
        /// コントローラーへの効率的なアクセス方法
        /// </summary>
        /// <param name="hashCode">GameObjectのハッシュ値</param>
        /// <returns>BaseControllerのインスタンス</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public BaseController GetBaseControllerByHash(int hashCode)
        {
            int bucketIndex = this.GetBucketIndex(hashCode);
            int entryIndex = this._buckets[bucketIndex];

            while ( entryIndex != -1 )
            {
                ref Entry entry = ref this._entries.ElementAt(entryIndex);
                if ( entry.HashCode == hashCode )
                {
                    return this._controllers[entry.ValueIndex];
                }
                entryIndex = entry.NextInBucket;
            }

            throw new KeyNotFoundException($"HashCode {hashCode} not found in dictionary");
        }

        /// <summary>
        /// インデックスからBaseControllerを直接取得（値を返す）
        /// コントローラーへの最高速度アクセス、ただしmanaged型のためJobSystemでは使用不可
        /// </summary>
        /// <param name="index">内部配列のインデックス</param>
        /// <returns>BaseControllerのインスタンス</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public BaseController GetBaseControllerByIndex(int index)
        {
            if ( !this.IsValidIndex(index) )
            {
                throw new ArgumentOutOfRangeException(nameof(index), "Index is out of range or points to a removed entry");
            }

            return this._controllers[index];
        }

        /// <summary>
        /// インデックスでBaseControllerを設定（値を設定）
        /// コントローラーの直接設定、初期化やリセット時に使用
        /// </summary>
        /// <param name="index">内部配列のインデックス</param>
        /// <param name="controller">設定するBaseControllerのインスタンス</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetBaseControllerByIndex(int index, BaseController controller)
        {
            if ( !this.IsValidIndex(index) )
            {
                throw new ArgumentOutOfRangeException(nameof(index), "Index is out of range or points to a removed entry");
            }

            this._controllers[index] = controller;
        }

        #endregion

        #endregion

        #region 内部データ管理処理

        /// <summary>
        /// 内部配列のサイズを変更
        /// </summary>
        private void Resize(int newCapacity)
        {
            // 素数サイズに調整
            int newPrimeSize = this.GetNextPrimeSize(newCapacity);

            // 各コレクションのサイズを拡張（既存データを保持）
            this._entries.Resize(newPrimeSize, NativeArrayOptions.ClearMemory);
            this._characterBaseInfo.Resize(newPrimeSize, NativeArrayOptions.ClearMemory);
            this._characterAtkStatus.Resize(newPrimeSize, NativeArrayOptions.ClearMemory);
            this._characterDefStatus.Resize(newPrimeSize, NativeArrayOptions.ClearMemory);
            this._characterStateInfo.Resize(newPrimeSize, NativeArrayOptions.ClearMemory);
            this._moveStatus.Resize(newPrimeSize, NativeArrayOptions.ClearMemory);
            this._coldLog.Resize(newPrimeSize, NativeArrayOptions.ClearMemory);

            // コントローラー配列のリサイズ
            if ( this._controllers.Length < newPrimeSize )
            {
                Array.Resize(ref this._controllers, newPrimeSize);
            }

            // バケットリストを新しいサイズで作り直し、-1で初期化
            this._buckets.Resize(newPrimeSize, NativeArrayOptions.ClearMemory);

            for ( int i = 0; i < newPrimeSize; i++ )
            {
                this._buckets[i] = -1;
            }

            // すべてのエントリを再ハッシュ
            for ( int i = 0; i < this._count; i++ )
            {
                ref Entry entry = ref this._entries.ElementAt(i);
                if ( entry.IsOccupied )
                {
                    int bucket = this.GetBucketIndex(entry.HashCode);
                    entry.NextInBucket = this._buckets[bucket];
                    this._buckets[bucket] = i;
                }
            }
        }

        /// <summary>
        /// ハッシュコードからバケットインデックスを取得（単純モジュロ法）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int GetBucketIndex(int hashCode)
        {
            return (hashCode & 0x7FFFFFFF) % this._buckets.Length;
        }

        /// <summary>
        /// 指定サイズ以上の最小の素数を取得
        /// </summary>
        private int GetNextPrimeSize(int minSize)
        {
            int index = Array.BinarySearch(PrimeSizes, minSize);

            if ( index >= 0 )
            {
                return PrimeSizes[index];
            }
            else
            {
                int insertIndex = ~index;

                if ( insertIndex < PrimeSizes.Length )
                {
                    return PrimeSizes[insertIndex];
                }
                else
                {
                    return this.CalculateNextPrime(minSize);
                }
            }
        }

        /// <summary>
        /// 指定値以上の次の素数を計算
        /// </summary>
        private int CalculateNextPrime(int minSize)
        {
            int candidate = minSize;
            if ( candidate % 2 == 0 )
            {
                candidate++;
            }

            while ( !this.IsPrime(candidate) )
            {
                candidate += 2;
            }

            return candidate;
        }

        /// <summary>
        /// 素数判定
        /// </summary>
        private bool IsPrime(int number)
        {
            if ( number <= 1 )
            {
                return false;
            }

            if ( number == 2 || number == 3 )
            {
                return true;
            }

            if ( number % 2 == 0 || number % 3 == 0 )
            {
                return false;
            }

            int limit = (int)Math.Sqrt(number);
            for ( int i = 5; i <= limit; i += 6 )
            {
                if ( number % i == 0 || number % (i + 2) == 0 )
                {
                    return false;
                }
            }

            return true;
        }

        #endregion

        /// <summary>
        /// 指定したゲームオブジェクトのキーがディクショナリに存在するかどうかを確認
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
        /// 指定したハッシュコードがディクショナリに存在するかどうかを確認
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ContainsKeyByHash(int hashCode)
        {
            int bucketIndex = this.GetBucketIndex(hashCode);
            int entryIndex = this._buckets[bucketIndex];

            while ( entryIndex != -1 )
            {
                ref Entry entry = ref this._entries.ElementAt(entryIndex);
                if ( entry.HashCode == hashCode )
                {
                    return true;
                }

                entryIndex = entry.NextInBucket;
            }

            return false;
        }

        /// <summary>
        /// すべての有効なエントリに対して処理を実行
        /// </summary>
        public void ForEach(Action<int, CharacterBaseInfo, CharacterAtkStatus, CharacterDefStatus,
                                   CharacterStateInfo, MoveStatus, CharaColdLog, BaseController> action)
        {
            if ( action == null )
            {
                throw new ArgumentNullException(nameof(action));
            }

            for ( int i = 0; i < this._count; i++ )
            {
                if ( i < this._entries.Length && this._entries[i].IsOccupied )
                {
                    action(i,
                          this._characterBaseInfo[i],
                          this._characterAtkStatus[i],
                          this._characterDefStatus[i],
                          this._characterStateInfo[i],
                          this._moveStatus[i],
                          this._coldLog[i],
                          this._controllers[i]);
                }
            }
        }

        /// <summary>
        /// リソースを解放
        /// </summary>
        public void Dispose()
        {
            if ( this._isDisposed )
            {
                return;
            }

            this._buckets.Dispose();
            this._entries.Dispose();
            this._characterBaseInfo.Dispose();
            this._characterAtkStatus.Dispose();
            this._characterDefStatus.Dispose();
            this._characterStateInfo.Dispose();
            this._moveStatus.Dispose();
            this._coldLog.Dispose();

            // コントローラー配列は参照だけ切る
            this._controllers = null;

            this._isDisposed = true;
        }
    }
}