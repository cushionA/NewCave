using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace MyTool.Collection
{
    /// <summary>
    /// ノンアロケーションリスト - 事前に確保した配列容量内でGCなしの操作を提供
    /// </summary>
    /// <typeparam name="T">要素の型</typeparam>
    [StructLayout(LayoutKind.Auto)]
    [DebuggerTypeProxy(typeof(NonAllocationListDebugView<>))]
    [DebuggerDisplay("Count = {Count}, Capacity = {Capacity}")]
    public struct NonAllocationList<T> : IEnumerable<T>
    {
        private readonly T[] _buffer;
        private int _count;

        /// <summary>
        /// 指定された配列でリストを初期化
        /// </summary>
        /// <param name="buffer">使用する配列バッファ</param>
        /// <param name="isActive">真ならバッファの要素をリストとして保持する</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NonAllocationList(T[] buffer, bool isActive = false)
        {
            _buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
            _count = isActive ? buffer.Length : 0;
        }

        /// <summary>
        /// 指定されたサイズでリストを作成
        /// </summary>
        /// <param name="capacity">容量</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NonAllocationList(int capacity)
        {
            _buffer = new T[capacity];
            _count = 0;
        }

        /// <summary>
        /// 現在の要素数
        /// </summary>
        public readonly int Count
        {
            [Pure]
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _count;
        }

        /// <summary>
        /// 最大容量
        /// </summary>
        public readonly int Capacity
        {
            [Pure]
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _buffer?.Length ?? 0;
        }

        /// <summary>
        /// リストが空かどうか
        /// </summary>
        public readonly bool IsEmpty
        {
            [Pure]
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _count == 0;
        }

        /// <summary>
        /// リストが満杯かどうか
        /// </summary>
        public readonly bool IsFull
        {
            [Pure]
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _count >= Capacity;
        }

        /// <summary>
        /// インデクサによる要素アクセス
        /// </summary>
        /// <param name="index">インデックス</param>
        /// <returns>要素への参照</returns>
        public T this[int index]
        {
            [Pure]
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if ( (uint)index >= (uint)_count )
                    ThrowIndexOutOfRange();
                return _buffer[index];
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                if ( (uint)index >= (uint)_count )
                    ThrowIndexOutOfRange();
                _buffer[index] = value;
            }
        }

        /// <summary>
        /// 末尾に要素を追加
        /// </summary>
        /// <param name="item">追加する要素</param>
        /// <returns>追加に成功した場合true</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryAdd(T item)
        {
            if ( _count >= Capacity )
                return false;

            _buffer[_count++] = item;
            return true;
        }

        /// <summary>
        /// 末尾に要素を追加（例外発生版）
        /// </summary>
        /// <param name="item">追加する要素</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(T item)
        {
            if ( !TryAdd(item) )
                ThrowCapacityExceeded();
        }

        /// <summary>
        /// 指定位置に要素を挿入
        /// </summary>
        /// <param name="index">挿入位置</param>
        /// <param name="item">挿入する要素</param>
        /// <returns>挿入に成功した場合true</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryInsert(int index, T item)
        {
            if ( (uint)index > (uint)_count || _count >= Capacity )
                return false;

            if ( index < _count )
            {
                // 要素を右にシフト
                Array.Copy(_buffer, index, _buffer, index + 1, _count - index);
            }

            _buffer[index] = item;
            _count++;
            return true;
        }

        /// <summary>
        /// 指定位置に要素を挿入（例外発生版）
        /// </summary>
        /// <param name="index">挿入位置</param>
        /// <param name="item">挿入する要素</param>
        public void Insert(int index, T item)
        {
            if ( !TryInsert(index, item) )
            {
                if ( (uint)index > (uint)_count )
                    ThrowArgumentOutOfRange(nameof(index));
                ThrowCapacityExceeded();
            }
        }

        /// <summary>
        /// 指定インデックスの要素を削除
        /// </summary>
        /// <param name="index">削除するインデックス</param>
        /// <returns>削除に成功した場合true</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryRemoveAt(int index)
        {
            if ( (uint)index >= (uint)_count )
                return false;

            _count--;
            if ( index < _count )
            {
                // 要素を左にシフト
                Array.Copy(_buffer, index + 1, _buffer, index, _count - index);
            }

            // 参照型の場合、メモリリークを防ぐためクリア
            _buffer[_count] = default(T);
            return true;
        }

        /// <summary>
        /// 指定インデックスの要素を削除（例外発生版）
        /// </summary>
        /// <param name="index">削除するインデックス</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RemoveAt(int index)
        {
            if ( !TryRemoveAt(index) )
                ThrowArgumentOutOfRange(nameof(index));
        }

        /// <summary>
        /// 指定された値の最初の出現を削除
        /// </summary>
        /// <param name="item">削除する値</param>
        /// <returns>削除に成功した場合true</returns>
        public bool Remove(T item)
        {
            var index = IndexOf(item);
            if ( index >= 0 )
            {
                return TryRemoveAt(index);
            }
            return false;
        }

        /// <summary>
        /// 末尾の要素を削除
        /// </summary>
        /// <returns>削除に成功した場合true</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryRemoveLast()
        {
            if ( _count > 0 )
            {
                _count--;
                _buffer[_count] = default(T); // メモリリーク防止
                return true;
            }
            return false;
        }

        /// <summary>
        /// 末尾の要素を削除して値を取得
        /// </summary>
        /// <param name="item">削除された要素</param>
        /// <returns>削除に成功した場合true</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryPop(out T item)
        {
            if ( _count > 0 )
            {
                _count--;
                item = _buffer[_count];
                _buffer[_count] = default(T); // メモリリーク防止
                return true;
            }
            item = default(T);
            return false;
        }

        /// <summary>
        /// 全ての要素をクリア
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear()
        {
            // 参照型の場合、メモリリークを防ぐため明示的にクリア
            if ( _count > 0 )
            {
                if ( RuntimeHelpers.IsReferenceOrContainsReferences<T>() )
                {
                    Array.Clear(_buffer, 0, _count);
                }
                _count = 0;
            }
        }

        /// <summary>
        /// 指定された値が含まれているかチェック
        /// </summary>
        /// <param name="item">検索する値</param>
        /// <returns>含まれている場合true</returns>
        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly bool Contains(T item)
        {
            return IndexOf(item) >= 0;
        }

        /// <summary>
        /// 指定された値のインデックスを取得
        /// </summary>
        /// <param name="item">検索する値</param>
        /// <returns>見つかった場合はインデックス、見つからない場合は-1</returns>
        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly int IndexOf(T item)
        {
            return Array.IndexOf(_buffer, item, 0, _count);
        }

        /// <summary>
        /// 末尾の要素を取得（削除しない）
        /// </summary>
        /// <param name="item">末尾の要素</param>
        /// <returns>要素が存在する場合true</returns>
        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly bool TryPeek(out T item)
        {
            if ( _count > 0 )
            {
                item = _buffer[_count - 1];
                return true;
            }
            item = default(T);
            return false;
        }

        /// <summary>
        /// 先頭の要素を取得（削除しない）
        /// </summary>
        /// <param name="item">先頭の要素</param>
        /// <returns>要素が存在する場合true</returns>
        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly bool TryPeekFirst(out T item)
        {
            if ( _count > 0 )
            {
                item = _buffer[0];
                return true;
            }
            item = default(T);
            return false;
        }

        /// <summary>
        /// 別のコレクションから要素をコピー
        /// </summary>
        /// <param name="source">コピー元</param>
        /// <returns>コピーに成功した場合true</returns>
        public bool TryCopyFrom(IEnumerable<T> source)
        {
            if ( source == null )
                return false;

            Clear();

            foreach ( var item in source )
            {
                if ( !TryAdd(item) )
                    return false;
            }
            return true;
        }

        /// <summary>
        /// 配列として要素をコピー取得
        /// </summary>
        /// <returns>現在の要素の配列</returns>
        [Pure]
        public readonly T[] ToArray()
        {
            var result = new T[_count];
            if ( _count > 0 )
            {
                Array.Copy(_buffer, 0, result, 0, _count);
            }
            return result;
        }

        /// <summary>
        /// スパンを返すメソッド
        /// </summary>
        /// <returns>現在の要素のスパン</returns>
        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Span<T> AsSpan()
        {
            return _buffer.AsSpan(0, _count);
        }

        /// <summary>
        /// 読み取り専用スパンを返すメソッド
        /// </summary>
        /// <returns>現在の要素の読み取り専用スパン</returns>
        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly ReadOnlySpan<T> AsReadOnlySpan()
        {
            return _buffer.AsSpan(0, _count);
        }

        /// <summary>
        /// 列挙子を取得
        /// </summary>
        /// <returns>列挙子</returns>
        [Pure]
        public readonly IEnumerator<T> GetEnumerator()
        {
            for ( int i = 0; i < _count; i++ )
            {
                yield return _buffer[i];
            }
        }

        /// <summary>
        /// 非ジェネリック列挙子を取得
        /// </summary>
        /// <returns>列挙子</returns>
        [Pure]
        readonly IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        // ============================================
        // 例外生成メソッド（コールドパスに分離して最適化）
        // ============================================

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ThrowIndexOutOfRange()
        {
            throw new IndexOutOfRangeException("インデックスが範囲外です。");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ThrowCapacityExceeded()
        {
            throw new InvalidOperationException("容量が不足しています。");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ThrowArgumentOutOfRange(string paramName)
        {
            throw new ArgumentOutOfRangeException(paramName);
        }
    }

    /// <summary>
    /// デバッグ表示用のプロキシクラス
    /// </summary>
    /// <typeparam name="T">要素の型</typeparam>
    [DebuggerNonUserCode]
    internal sealed class NonAllocationListDebugView<T>
    {
        private readonly NonAllocationList<T> _list;

        public NonAllocationListDebugView(NonAllocationList<T> list)
        {
            _list = list;
        }

        [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
        public T[] Items
        {
            get
            {
                var items = new T[_list.Count];
                for ( int i = 0; i < _list.Count; i++ )
                {
                    items[i] = _list[i];
                }
                return items;
            }
        }
    }
}