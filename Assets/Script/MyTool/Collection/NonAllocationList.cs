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
    /// �m���A���P�[�V�������X�g - ���O�Ɋm�ۂ����z��e�ʓ���GC�Ȃ��̑�����
    /// </summary>
    /// <typeparam name="T">�v�f�̌^</typeparam>
    [StructLayout(LayoutKind.Auto)]
    [DebuggerTypeProxy(typeof(NonAllocationListDebugView<>))]
    [DebuggerDisplay("Count = {Count}, Capacity = {Capacity}")]
    public struct NonAllocationList<T> : IEnumerable<T>
    {
        private readonly T[] _buffer;
        private int _count;

        /// <summary>
        /// �w�肳�ꂽ�z��Ń��X�g��������
        /// </summary>
        /// <param name="buffer">�g�p����z��o�b�t�@</param>
        /// <param name="isActive">�^�Ȃ�o�b�t�@�̗v�f�����X�g�Ƃ��ĕێ�����</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NonAllocationList(T[] buffer, bool isActive = false)
        {
            _buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
            _count = isActive ? buffer.Length : 0;
        }

        /// <summary>
        /// �w�肳�ꂽ�T�C�Y�Ń��X�g���쐬
        /// </summary>
        /// <param name="capacity">�e��</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NonAllocationList(int capacity)
        {
            _buffer = new T[capacity];
            _count = 0;
        }

        /// <summary>
        /// ���݂̗v�f��
        /// </summary>
        public readonly int Count
        {
            [Pure]
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _count;
        }

        /// <summary>
        /// �ő�e��
        /// </summary>
        public readonly int Capacity
        {
            [Pure]
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _buffer?.Length ?? 0;
        }

        /// <summary>
        /// ���X�g���󂩂ǂ���
        /// </summary>
        public readonly bool IsEmpty
        {
            [Pure]
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _count == 0;
        }

        /// <summary>
        /// ���X�g�����t���ǂ���
        /// </summary>
        public readonly bool IsFull
        {
            [Pure]
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _count >= Capacity;
        }

        /// <summary>
        /// �C���f�N�T�ɂ��v�f�A�N�Z�X
        /// </summary>
        /// <param name="index">�C���f�b�N�X</param>
        /// <returns>�v�f�ւ̎Q��</returns>
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
        /// �����ɗv�f��ǉ�
        /// </summary>
        /// <param name="item">�ǉ�����v�f</param>
        /// <returns>�ǉ��ɐ��������ꍇtrue</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryAdd(T item)
        {
            if ( _count >= Capacity )
                return false;

            _buffer[_count++] = item;
            return true;
        }

        /// <summary>
        /// �����ɗv�f��ǉ��i��O�����Łj
        /// </summary>
        /// <param name="item">�ǉ�����v�f</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(T item)
        {
            if ( !TryAdd(item) )
                ThrowCapacityExceeded();
        }

        /// <summary>
        /// �w��ʒu�ɗv�f��}��
        /// </summary>
        /// <param name="index">�}���ʒu</param>
        /// <param name="item">�}������v�f</param>
        /// <returns>�}���ɐ��������ꍇtrue</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryInsert(int index, T item)
        {
            if ( (uint)index > (uint)_count || _count >= Capacity )
                return false;

            if ( index < _count )
            {
                // �v�f���E�ɃV�t�g
                Array.Copy(_buffer, index, _buffer, index + 1, _count - index);
            }

            _buffer[index] = item;
            _count++;
            return true;
        }

        /// <summary>
        /// �w��ʒu�ɗv�f��}���i��O�����Łj
        /// </summary>
        /// <param name="index">�}���ʒu</param>
        /// <param name="item">�}������v�f</param>
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
        /// �w��C���f�b�N�X�̗v�f���폜
        /// </summary>
        /// <param name="index">�폜����C���f�b�N�X</param>
        /// <returns>�폜�ɐ��������ꍇtrue</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryRemoveAt(int index)
        {
            if ( (uint)index >= (uint)_count )
                return false;

            _count--;
            if ( index < _count )
            {
                // �v�f�����ɃV�t�g
                Array.Copy(_buffer, index + 1, _buffer, index, _count - index);
            }

            // �Q�ƌ^�̏ꍇ�A���������[�N��h�����߃N���A
            _buffer[_count] = default(T);
            return true;
        }

        /// <summary>
        /// �w��C���f�b�N�X�̗v�f���폜�i��O�����Łj
        /// </summary>
        /// <param name="index">�폜����C���f�b�N�X</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RemoveAt(int index)
        {
            if ( !TryRemoveAt(index) )
                ThrowArgumentOutOfRange(nameof(index));
        }

        /// <summary>
        /// �w�肳�ꂽ�l�̍ŏ��̏o�����폜
        /// </summary>
        /// <param name="item">�폜����l</param>
        /// <returns>�폜�ɐ��������ꍇtrue</returns>
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
        /// �����̗v�f���폜
        /// </summary>
        /// <returns>�폜�ɐ��������ꍇtrue</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryRemoveLast()
        {
            if ( _count > 0 )
            {
                _count--;
                _buffer[_count] = default(T); // ���������[�N�h�~
                return true;
            }
            return false;
        }

        /// <summary>
        /// �����̗v�f���폜���Ēl���擾
        /// </summary>
        /// <param name="item">�폜���ꂽ�v�f</param>
        /// <returns>�폜�ɐ��������ꍇtrue</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryPop(out T item)
        {
            if ( _count > 0 )
            {
                _count--;
                item = _buffer[_count];
                _buffer[_count] = default(T); // ���������[�N�h�~
                return true;
            }
            item = default(T);
            return false;
        }

        /// <summary>
        /// �S�Ă̗v�f���N���A
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear()
        {
            // �Q�ƌ^�̏ꍇ�A���������[�N��h�����ߖ����I�ɃN���A
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
        /// �w�肳�ꂽ�l���܂܂�Ă��邩�`�F�b�N
        /// </summary>
        /// <param name="item">��������l</param>
        /// <returns>�܂܂�Ă���ꍇtrue</returns>
        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly bool Contains(T item)
        {
            return IndexOf(item) >= 0;
        }

        /// <summary>
        /// �w�肳�ꂽ�l�̃C���f�b�N�X���擾
        /// </summary>
        /// <param name="item">��������l</param>
        /// <returns>���������ꍇ�̓C���f�b�N�X�A������Ȃ��ꍇ��-1</returns>
        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly int IndexOf(T item)
        {
            return Array.IndexOf(_buffer, item, 0, _count);
        }

        /// <summary>
        /// �����̗v�f���擾�i�폜���Ȃ��j
        /// </summary>
        /// <param name="item">�����̗v�f</param>
        /// <returns>�v�f�����݂���ꍇtrue</returns>
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
        /// �擪�̗v�f���擾�i�폜���Ȃ��j
        /// </summary>
        /// <param name="item">�擪�̗v�f</param>
        /// <returns>�v�f�����݂���ꍇtrue</returns>
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
        /// �ʂ̃R���N�V��������v�f���R�s�[
        /// </summary>
        /// <param name="source">�R�s�[��</param>
        /// <returns>�R�s�[�ɐ��������ꍇtrue</returns>
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
        /// �z��Ƃ��ėv�f���R�s�[�擾
        /// </summary>
        /// <returns>���݂̗v�f�̔z��</returns>
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
        /// �X�p����Ԃ����\�b�h
        /// </summary>
        /// <returns>���݂̗v�f�̃X�p��</returns>
        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Span<T> AsSpan()
        {
            return _buffer.AsSpan(0, _count);
        }

        /// <summary>
        /// �ǂݎ���p�X�p����Ԃ����\�b�h
        /// </summary>
        /// <returns>���݂̗v�f�̓ǂݎ���p�X�p��</returns>
        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly ReadOnlySpan<T> AsReadOnlySpan()
        {
            return _buffer.AsSpan(0, _count);
        }

        /// <summary>
        /// �񋓎q���擾
        /// </summary>
        /// <returns>�񋓎q</returns>
        [Pure]
        public readonly IEnumerator<T> GetEnumerator()
        {
            for ( int i = 0; i < _count; i++ )
            {
                yield return _buffer[i];
            }
        }

        /// <summary>
        /// ��W�F�l���b�N�񋓎q���擾
        /// </summary>
        /// <returns>�񋓎q</returns>
        [Pure]
        readonly IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        // ============================================
        // ��O�������\�b�h�i�R�[���h�p�X�ɕ������čœK���j
        // ============================================

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ThrowIndexOutOfRange()
        {
            throw new IndexOutOfRangeException("�C���f�b�N�X���͈͊O�ł��B");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ThrowCapacityExceeded()
        {
            throw new InvalidOperationException("�e�ʂ��s�����Ă��܂��B");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ThrowArgumentOutOfRange(string paramName)
        {
            throw new ArgumentOutOfRangeException(paramName);
        }
    }

    /// <summary>
    /// �f�o�b�O�\���p�̃v���L�V�N���X
    /// </summary>
    /// <typeparam name="T">�v�f�̌^</typeparam>
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