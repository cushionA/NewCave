using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace MyTool.Collections
{

    #region T用インターフェイス

    /// <summary>
    /// 論理削除を実装するインターフェイス。<br/>
    /// このコレクションでは論理削除を採用していて、さらに内部バッファを外に持ち出すため<br/>
    /// コレクション内の要素に論理削除の対応をさせる必要がある。<br/>
    /// Tの要素はnull非許容であるため、誤ってアクセスしないようにインターフェイスの実装を強制。<br/>
    /// </summary>
    public interface ILogicalDelate
    {
        /// <summary>
        /// 論理削除されているかどうかを判断するメソッド。
        /// </summary>
        /// <returns></returns>
        bool IsLogicalDelate();

        /// <summary>
        /// 論理削除するメソッド。
        /// </summary>
        void LogicalDelete();
    }

    #endregion T用インターフェイス

    /// <summary>
    /// ゲームオブジェクトのGetHashCode()をキーとして管理するデータ辞書
    /// UnityではGetHashCode()がGetInstanceID()と同じ値を返すため、キーの一意性が保証されている。
    /// 内部でのデータ保持にUnsafeListを使用してGC負荷を削減
    /// 
    /// CharaDataDic<T>との違いは、T2を追加して、キャラコントローラーも一緒に操作できるようにしたということだけ。
    /// 自作ゲーム用にT2を追加して特化させたのがCharacterDataContainer<T1, T2>。
    /// あとT2はジェネリック型にする意味があまりない（そのゲームで使うキャラクターコントローラーの型だから）いずれこの方針で最適化する
    /// </summary>
    /// <typeparam name="T1">格納する主データの型（JobSystemを意識したunmanaged制約付き）</typeparam>
    /// <typeparam name="T2">格納する副データの型（Unmanaged制約なしのキャラクターコントローラー）</typeparam>
    public class CharacterDataContainer<T1, T2> : IDisposable
        where T1 : unmanaged, ILogicalDelate
        where T2 : class
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
        private UnsafeList<int> _buckets;

        /// <summary>
        /// エントリのリスト
        /// </summary>
        private UnsafeList<Entry> _entries;

        /// <summary>
        /// 実際のデータT1を格納するリスト
        /// </summary>
        private UnsafeList<T1> _values1;

        /// <summary>
        /// 実際のデータT2を格納する配列（managed型のため通常の配列）
        /// </summary>
        private T2[] _values2;

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

        #region T1インデクサ

        /// <summary>
        /// インデクサ - ゲームオブジェクトからの値アクセス (T1)
        /// </summary>
        public T1 this[GameObject gameObject]
        {
            get
            {
                if ( gameObject == null )
                {
                    throw new ArgumentNullException(nameof(gameObject));
                }

                if ( this.TryGetValue(gameObject, out T1 value1, out _) )
                {
                    return value1;
                }

                throw new KeyNotFoundException($"GameObject {gameObject.name} not found in store");
            }
            set => this.Add(gameObject, value);  // このインデクサからの追加はT1のみの追加になる点に注意
        }

        /// <summary>
        /// インデクサ - ハッシュコード/インスタンスIDからの値アクセス (T1)
        /// </summary>
        public T1 this[int hashOrInstanceId]
        {
            get
            {
                if ( this.TryGetValueByHash(hashOrInstanceId, out T1 value1, out _) )
                {
                    return value1;
                }

                throw new KeyNotFoundException($"HashCode/InstanceID {hashOrInstanceId.ToString()} not found in store");
            }
            set => this.AddByHash(hashOrInstanceId, value);  // このインデクサからの追加はT1のみの追加になる点に注意
        }

        /// <summary>
        /// インデクサ - 値インデックスからの直接アクセス (T1)
        /// </summary>

        public T1 this[int valueIndex, bool isValueIndex]
        {
            get
            {
                if ( !isValueIndex )
                {
                    throw new ArgumentException("Second parameter must be true when accessing by value index");
                }

                if ( valueIndex < 0 || valueIndex >= this._count || !this.IsValidIndex(valueIndex) )
                {
                    throw new ArgumentOutOfRangeException(nameof(valueIndex));
                }

                return this._values1[valueIndex];
            }
        }

        #endregion

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="capacity">初期容量（素数に調整されます）</param>
        /// <param name="allocator">メモリアロケータ（デフォルトはPersistent）</param>
        public CharacterDataContainer(int capacity = DEFAULT_CAPACITY, Allocator allocator = Allocator.Persistent)
        {
            // アロケータ保存
            this._allocator = allocator;

            // 指定容量以上の最小の素数を選択
            int primeCapacity = this.GetNextPrimeSize(capacity);

            // UnsafeListの初期化
            this._buckets = new UnsafeList<int>(primeCapacity, allocator);
            this._entries = new UnsafeList<Entry>(primeCapacity, allocator);
            this._values1 = new UnsafeList<T1>(primeCapacity, allocator);
            this._values2 = new T2[primeCapacity]; // managed型のため通常の配列を使用

            // バケットリストの容量確保と-1で初期化
            this._buckets.Resize(primeCapacity, NativeArrayOptions.ClearMemory);
            for ( int i = 0; i < this._buckets.Length; i++ )
            {
                this._buckets[i] = -1;
            }

            // 他のリストの容量を確保
            this._entries.Capacity = primeCapacity;
            this._values1.Capacity = primeCapacity;
            // _values2は既に配列で初期化済み

            this._freeListHead = -1;
            this._count = 0;
            this._freeCount = 0;
        }

        /// <summary>
        /// ゲームオブジェクトとデータを追加または更新 (T1のみ)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Add(GameObject obj, T1 data)
        {
            if ( obj == null )
            {
                throw new ArgumentNullException(nameof(obj));
            }

            // GetHashCode()を使用（GetInstanceID()と同じ値）
            return this.AddByHash(obj.GetHashCode(), data);
        }

        /// <summary>
        /// ゲームオブジェクトとデータを追加または更新 (T1とT2)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Add(GameObject obj, T1 data1, T2 data2)
        {
            if ( obj == null )
            {
                throw new ArgumentNullException(nameof(obj));
            }

            // GetHashCode()を使用（GetInstanceID()と同じ値）
            return this.AddByHash(obj.GetHashCode(), data1, data2);
        }

        /// <summary>
        /// ハッシュコード/インスタンスIDとデータを追加または更新し、値のインデックスを返す (T1のみ)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int AddByHash(int hashCode, T1 data)
        {
            // T2にはデフォルト値を設定
            return this.AddByHash(hashCode, data, default);
        }

        /// <summary>
        /// ハッシュコード/インスタンスIDとデータを追加または更新し、値のインデックスを返す (T1とT2)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int AddByHash(int hashCode, T1 data1, T2 data2)
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
                // ハッシュコードのみでチェック（GetInstanceID()と同じ値なので一意性が保証される）
                if ( entry.HashCode == hashCode )
                {
                    // 既存エントリを更新
                    this._values1[entry.ValueIndex] = data1;
                    this._values2[entry.ValueIndex] = data2;
                    return entry.ValueIndex;
                }

                // ハッシュコードが重複しないが、同じバケットに要素がある場合はバケットないの次の要素として保存。
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

            // UnsafeListの更新
            if ( newIndex < this._entries.Length )
            {
                this._entries[newIndex] = newEntry;
            }
            else
            {
                this._entries.Add(newEntry);
            }

            if ( newIndex < this._values1.Length )
            {
                this._values1[newIndex] = data1;
            }
            else
            {
                this._values1.Add(data1);
            }

            // T2はmanaged型の配列なので単純に代入
            this._values2[newIndex] = data2;

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

            if ( this._values1.Capacity <= index )
            {
                int newCapacity = Math.Max(this._values1.Capacity * 2, index + 1);
                this._values1.Capacity = newCapacity;
            }

            if ( this._values2.Length <= index )
            {
                int newCapacity = Math.Max(this._values2.Length * 2, index + 1);
                T2[] newArray = new T2[newCapacity];
                Array.Copy(this._values2, newArray, this._values2.Length);
                this._values2 = newArray;
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
        /// インデックスから直接データT1を取得（参照を返す）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref T1 GetData1ByIndex(int index)
        {
            if ( !this.IsValidIndex(index) )
            {
                throw new ArgumentOutOfRangeException(nameof(index), "Index is out of range or points to a removed entry");
            }

            return ref this._values1.ElementAt(index);
        }

        /// <summary>
        /// インデックスから直接データT2を取得
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T2 GetData2ByIndex(int index)
        {
            if ( !this.IsValidIndex(index) )
            {
                throw new ArgumentOutOfRangeException(nameof(index), "Index is out of range or points to a removed entry");
            }

            return this._values2[index];
        }

        /// <summary>
        /// インデックスから直接データT2を設定
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetData2ByIndex(int index, T2 value)
        {
            if ( !this.IsValidIndex(index) )
            {
                throw new ArgumentOutOfRangeException(nameof(index), "Index is out of range or points to a removed entry");
            }

            this._values2[index] = value;
        }

        /// <summary>
        /// ゲームオブジェクトからデータT1, T2と内部インデックスを取得
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetValue(GameObject obj, out T1 data1, out T2 data2, out int index)
        {
            if ( obj == null )
            {
                data1 = default;
                data2 = null;
                index = -1;
                return false;
            }

            // GetHashCode()を使用（GetInstanceID()と同じ値）
            return this.TryGetValueByHash(obj.GetHashCode(), out data1, out data2, out index);
        }

        /// <summary>
        /// ゲームオブジェクトからデータT1と内部インデックスを取得
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetValue(GameObject obj, out T1 data1, out int index)
        {
            if ( obj == null )
            {
                data1 = default;
                index = -1;
                return false;
            }

            // GetHashCode()を使用（GetInstanceID()と同じ値）
            return this.TryGetValueByHash(obj.GetHashCode(), out data1, out index);
        }

        /// <summary>
        /// ゲームオブジェクトからデータT2と内部インデックスを取得
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetValue(GameObject obj, out T2 data2, out int index)
        {
            if ( obj == null )
            {
                data2 = null;
                index = -1;
                return false;
            }

            // GetHashCode()を使用（GetInstanceID()と同じ値）
            return this.TryGetValueByHash(obj.GetHashCode(), out data2, out index);
        }

        /// <summary>
        /// ハッシュコード/インスタンスIDからデータT1, T2と内部インデックスを取得
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetValueByHash(int hashCode, out T1 data1, out T2 data2, out int index)
        {
            int bucketIndex = this.GetBucketIndex(hashCode);
            int entryIndex = this._buckets[bucketIndex];

            while ( entryIndex != -1 )
            {
                ref Entry entry = ref this._entries.ElementAt(entryIndex);
                if ( entry.HashCode == hashCode )
                {
                    data1 = this._values1[entry.ValueIndex];
                    data2 = this._values2[entry.ValueIndex];
                    index = entry.ValueIndex;
                    return true;
                }

                entryIndex = entry.NextInBucket;
            }

            data1 = default;
            data2 = null;
            index = -1;
            return false;
        }

        /// <summary>
        /// ハッシュコード/インスタンスIDからデータT1と内部インデックスを取得
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetValueByHash(int hashCode, out T1 data1, out int index)
        {
            int bucketIndex = this.GetBucketIndex(hashCode);
            int entryIndex = this._buckets[bucketIndex];

            while ( entryIndex != -1 )
            {
                ref Entry entry = ref this._entries.ElementAt(entryIndex);
                if ( entry.HashCode == hashCode )
                {
                    data1 = this._values1[entry.ValueIndex];
                    index = entry.ValueIndex;
                    return true;
                }

                entryIndex = entry.NextInBucket;
            }

            data1 = default;
            index = -1;
            return false;
        }

        /// <summary>
        /// ハッシュコード/インスタンスIDからT2と内部インデックスを取得
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetValueByHash(int hashCode, out T2 data2, out int index)
        {
            int bucketIndex = this.GetBucketIndex(hashCode);
            int entryIndex = this._buckets[bucketIndex];

            while ( entryIndex != -1 )
            {
                ref Entry entry = ref this._entries.ElementAt(entryIndex);
                if ( entry.HashCode == hashCode )
                {
                    data2 = this._values2[entry.ValueIndex];
                    index = entry.ValueIndex;
                    return true;
                }

                entryIndex = entry.NextInBucket;
            }

            data2 = null;
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

            // GetHashCode()を使用（GetInstanceID()と同じ値）
            return this.RemoveByHash(obj.GetHashCode());
        }

        /// <summary>
        /// ハッシュコード/インスタンスIDに関連付けられたデータを削除
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
                    // エントリの要素を論理削除する。
                    this._values1[entry.ValueIndex].LogicalDelete();
                    this._values2[entry.ValueIndex] = null;

                    // エントリをバケットリストから削除
                    if ( prevIndex != -1 )
                    {
                        // 前のエントリの次のリンクを更新
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

            // UnsafeListをクリア
            this._entries.Clear();
            this._values1.Clear();

            this._count = 0;
            this._freeCount = 0;
            this._freeListHead = -1;
        }

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
            this._values1.Resize(newPrimeSize, NativeArrayOptions.ClearMemory);

            // managed型配列のリサイズ
            if ( this._values2.Length < newPrimeSize )
            {
                Array.Resize(ref this._values2, newPrimeSize);
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
            // ハッシュコードは負の値もあり得るので、絶対値を高速に取得
            // hashCode & 0x7FFFFFFF が Math.Abs(hashCode) より高速
            // 絶対値でModをとることで 0～要素数-1、の間のインデックスを取得できる
            return (hashCode & 0x7FFFFFFF) % this._buckets.Length;
        }

        /// <summary>
        /// 指定サイズ以上の最小の素数を取得
        /// </summary>
        private int GetNextPrimeSize(int minSize)
        {
            // バイナリサーチで素数テーブルから適切な値を探す
            int index = Array.BinarySearch(PrimeSizes, minSize);

            if ( index >= 0 )
            {
                // ぴったり一致する素数が見つかった
                return PrimeSizes[index];
            }
            else
            {
                // 一致する値がない場合、~index は挿入すべき位置を表す
                int insertIndex = ~index;

                if ( insertIndex < PrimeSizes.Length )
                {
                    // テーブル内にある要求した値より大きい素数を返す
                    return PrimeSizes[insertIndex];
                }
                else
                {
                    // テーブル内の最大値より大きい場合は計算する
                    return this.CalculateNextPrime(minSize);
                }
            }
        }

        /// <summary>
        /// 指定値以上の次の素数を計算
        /// </summary>
        private int CalculateNextPrime(int minSize)
        {
            // 奇数から開始（偶数は2以外素数にならない）
            int candidate = minSize;
            if ( candidate % 2 == 0 )
            {
                candidate++;
            }

            while ( !this.IsPrime(candidate) )
            {
                candidate += 2; // 奇数のみをチェック
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

            // 6k±1の形で表される数のみチェック（効率化）
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

        #endregion 内部データ管理処理

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
        /// 指定したハッシュコード/インスタンスIDがディクショナリに存在するかどうかを確認
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
        /// すべての有効なエントリに対して処理を実行 (T1のみ)。<br></br>
        /// IEnumerableの代わり
        /// </summary>
        public void ForEach(Action<int, T1> action)
        {
            if ( action == null )
            {
                throw new ArgumentNullException(nameof(action));
            }

            for ( int i = 0; i < this._count; i++ )
            {
                if ( i < this._entries.Length && this._entries[i].IsOccupied )
                {
                    action(i, this._values1.ElementAt(i));
                }
            }
        }

        /// <summary>
        /// すべての有効なエントリに対して処理を実行 (T1とT2)。<br></br>
        /// IEnumerableの代わり
        /// </summary>
        public void ForEach(Action<int, T1, T2> action)
        {
            if ( action == null )
            {
                throw new ArgumentNullException(nameof(action));
            }

            for ( int i = 0; i < this._count; i++ )
            {
                if ( i < this._entries.Length && this._entries[i].IsOccupied )
                {
                    action(i, this._values1.ElementAt(i), this._values2.ElementAt(i));
                }
            }
        }

        /// <summary>
        /// ジョブシステムでキャラクターデータT1を使用するためにリストを内部から取得する。<br></br>
        /// 絶対にここで受け取ったリストをDisposeしてはならない。この自作Dictionaryはゲーム終了時に破棄する。<br></br>
        /// また、意図せず参照が残らないようにローカル変数以外で受け取ってもだめ。<br></br>
        /// ReadOnlyにしたいところだけど、そうするといろいろ使いにくいから仕方ない。
        /// </summary>
        /// <returns>T1データのUnsafeList</returns>
        public UnsafeList<T1> GetInternalList1ForJob()
        {
            // 内部リストを返す
            return this._values1;
        }

        /// <summary>
        /// キャラクターデータT2を使用するために内部配列への参照を取得する。<br></br>
        /// T2がmanaged型のため、JobSystemでは使用できない。<br></br>
        /// ReadOnlyにしたいところだけど、そうするといろいろ使いにくいから仕方ない。
        /// </summary>
        /// <returns>T2データのSpan</returns>
        public Span<T2> GetInternalArray2()
        {
            // 内部配列を返す
            return this._values2.AsSpan();
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
            this._values1.Dispose();
            // _values2はmanaged型の配列なので参照だけ切る。
            this._values2 = null;

            this._isDisposed = true;
        }
    }
}
