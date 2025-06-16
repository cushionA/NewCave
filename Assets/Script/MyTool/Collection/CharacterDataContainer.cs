using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace MyTool.Collections
{

    #region T�p�C���^�[�t�F�C�X

    /// <summary>
    /// �_���폜����������C���^�[�t�F�C�X�B<br/>
    /// ���̃R���N�V�����ł͘_���폜���̗p���Ă��āA����ɓ����o�b�t�@���O�Ɏ����o������<br/>
    /// �R���N�V�������̗v�f�ɘ_���폜�̑Ή���������K�v������B<br/>
    /// T�̗v�f��null�񋖗e�ł��邽�߁A����ăA�N�Z�X���Ȃ��悤�ɃC���^�[�t�F�C�X�̎����������B<br/>
    /// </summary>
    public interface ILogicalDelate
    {
        /// <summary>
        /// �_���폜����Ă��邩�ǂ����𔻒f���郁�\�b�h�B
        /// </summary>
        /// <returns></returns>
        bool IsLogicalDelate();

        /// <summary>
        /// �_���폜���郁�\�b�h�B
        /// </summary>
        void LogicalDelete();
    }

    #endregion T�p�C���^�[�t�F�C�X

    /// <summary>
    /// �Q�[���I�u�W�F�N�g��GetHashCode()���L�[�Ƃ��ĊǗ�����f�[�^����
    /// Unity�ł�GetHashCode()��GetInstanceID()�Ɠ����l��Ԃ����߁A�L�[�̈�Ӑ����ۏ؂���Ă���B
    /// �����ł̃f�[�^�ێ���UnsafeList���g�p����GC���ׂ��팸
    /// 
    /// CharaDataDic<T>�Ƃ̈Ⴂ�́AT2��ǉ����āA�L�����R���g���[���[���ꏏ�ɑ���ł���悤�ɂ����Ƃ������Ƃ����B
    /// ����Q�[���p��T2��ǉ����ē����������̂�CharacterDataContainer<T1, T2>�B
    /// ����T2�̓W�F�l���b�N�^�ɂ���Ӗ������܂�Ȃ��i���̃Q�[���Ŏg���L�����N�^�[�R���g���[���[�̌^������j�����ꂱ�̕��j�ōœK������
    /// </summary>
    /// <typeparam name="T1">�i�[�����f�[�^�̌^�iJobSystem���ӎ�����unmanaged����t���j</typeparam>
    /// <typeparam name="T2">�i�[���镛�f�[�^�̌^�iUnmanaged����Ȃ��̃L�����N�^�[�R���g���[���[�j</typeparam>
    public class CharacterDataContainer<T1, T2> : IDisposable
        where T1 : unmanaged, ILogicalDelate
        where T2 : class
    {
        /// <summary>
        /// �G���g���\���� - �L�[�ƒl�ƃ`�F�[�������i�[<br></br>
        /// �n�b�V���R�[�h���o�P�b�gID���G���g�������ۂ̃C���f�b�N�X�Ƃ�������B
        /// </summary>
        private struct Entry
        {
            /// <summary>
            /// �Q�[���I�u�W�F�N�g�̃n�b�V���R�[�h�iGetInstanceID()�Ɠ���j
            /// </summary>
            public int HashCode;

            /// <summary>
            /// �l�z����̃C���f�b�N�X
            /// </summary>
            public int ValueIndex;

            /// <summary>
            /// �����o�P�b�g���̎��̃G���g���ւ̃C���f�b�N�X�B
            /// </summary>
            public int NextInBucket;

            /// <summary>
            /// ���̃G���g�����g�p�����ǂ���
            /// </summary>
            public bool IsOccupied;
        }

        /// <summary>
        /// �o�P�b�g�z��i�e�v�f�̓G���g���ւ̃C���f�b�N�X�A-1�͋�j
        /// </summary>
        private UnsafeList<int> _buckets;

        /// <summary>
        /// �G���g���̃��X�g
        /// </summary>
        private UnsafeList<Entry> _entries;

        /// <summary>
        /// ���ۂ̃f�[�^T1���i�[���郊�X�g
        /// </summary>
        private UnsafeList<T1> _values1;

        /// <summary>
        /// ���ۂ̃f�[�^T2���i�[����z��imanaged�^�̂��ߒʏ�̔z��j
        /// </summary>
        private T2[] _values2;

        /// <summary>
        /// �g�p���̃G���g����
        /// </summary>
        private int _count;

        /// <summary>
        /// �폜�ς݃G���g���̍ė��p���X�g�擪
        /// </summary>
        private int _freeListHead;

        /// <summary>
        /// �ė��p�\�ȃG���g����
        /// </summary>
        private int _freeCount;

        /// <summary>
        /// ����ς݃t���O
        /// </summary>
        private bool _isDisposed;

        /// <summary>
        /// �������̊m�ۂ̃^�C�v
        /// </summary>
        private readonly Allocator _allocator;

        /// <summary>
        /// �f���e�[�u��
        /// ���T�C�Y�̍ۂɎg�p����B
        /// </summary>
        private static readonly int[] PrimeSizes = {
        17, 37, 79, 163, 331, 673, 1361, 2729, 5471, 10949, 21911, 43853,
        87719, 175447, 350899, 701819, 1403641, 2807303, 5614657, 11229331
    };

        /// <summary>
        /// �����T�C�Y�萔
        /// </summary>
        private const int DEFAULT_CAPACITY = 1031;

        /// <summary>
        /// ���׌W���i���e�ςݗv�f�̊��������̒l�𒴂���ƃ��T�C�Y�j
        /// </summary>
        private const float LOAD_FACTOR = 0.75f;

        /// <summary>
        /// �i�[����Ă���v�f��
        /// </summary>
        public int Count => this._count - this._freeCount;

        /// <summary>
        /// �o�P�b�g�̗e��
        /// </summary>
        public int Capacity => this._entries.Length;

        #region T1�C���f�N�T

        /// <summary>
        /// �C���f�N�T - �Q�[���I�u�W�F�N�g����̒l�A�N�Z�X (T1)
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
            set => this.Add(gameObject, value);  // ���̃C���f�N�T����̒ǉ���T1�݂̂̒ǉ��ɂȂ�_�ɒ���
        }

        /// <summary>
        /// �C���f�N�T - �n�b�V���R�[�h/�C���X�^���XID����̒l�A�N�Z�X (T1)
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
            set => this.AddByHash(hashOrInstanceId, value);  // ���̃C���f�N�T����̒ǉ���T1�݂̂̒ǉ��ɂȂ�_�ɒ���
        }

        /// <summary>
        /// �C���f�N�T - �l�C���f�b�N�X����̒��ڃA�N�Z�X (T1)
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
        /// �R���X�g���N�^
        /// </summary>
        /// <param name="capacity">�����e�ʁi�f���ɒ�������܂��j</param>
        /// <param name="allocator">�������A���P�[�^�i�f�t�H���g��Persistent�j</param>
        public CharacterDataContainer(int capacity = DEFAULT_CAPACITY, Allocator allocator = Allocator.Persistent)
        {
            // �A���P�[�^�ۑ�
            this._allocator = allocator;

            // �w��e�ʈȏ�̍ŏ��̑f����I��
            int primeCapacity = this.GetNextPrimeSize(capacity);

            // UnsafeList�̏�����
            this._buckets = new UnsafeList<int>(primeCapacity, allocator);
            this._entries = new UnsafeList<Entry>(primeCapacity, allocator);
            this._values1 = new UnsafeList<T1>(primeCapacity, allocator);
            this._values2 = new T2[primeCapacity]; // managed�^�̂��ߒʏ�̔z����g�p

            // �o�P�b�g���X�g�̗e�ʊm�ۂ�-1�ŏ�����
            this._buckets.Resize(primeCapacity, NativeArrayOptions.ClearMemory);
            for ( int i = 0; i < this._buckets.Length; i++ )
            {
                this._buckets[i] = -1;
            }

            // ���̃��X�g�̗e�ʂ��m��
            this._entries.Capacity = primeCapacity;
            this._values1.Capacity = primeCapacity;
            // _values2�͊��ɔz��ŏ������ς�

            this._freeListHead = -1;
            this._count = 0;
            this._freeCount = 0;
        }

        /// <summary>
        /// �Q�[���I�u�W�F�N�g�ƃf�[�^��ǉ��܂��͍X�V (T1�̂�)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Add(GameObject obj, T1 data)
        {
            if ( obj == null )
            {
                throw new ArgumentNullException(nameof(obj));
            }

            // GetHashCode()���g�p�iGetInstanceID()�Ɠ����l�j
            return this.AddByHash(obj.GetHashCode(), data);
        }

        /// <summary>
        /// �Q�[���I�u�W�F�N�g�ƃf�[�^��ǉ��܂��͍X�V (T1��T2)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Add(GameObject obj, T1 data1, T2 data2)
        {
            if ( obj == null )
            {
                throw new ArgumentNullException(nameof(obj));
            }

            // GetHashCode()���g�p�iGetInstanceID()�Ɠ����l�j
            return this.AddByHash(obj.GetHashCode(), data1, data2);
        }

        /// <summary>
        /// �n�b�V���R�[�h/�C���X�^���XID�ƃf�[�^��ǉ��܂��͍X�V���A�l�̃C���f�b�N�X��Ԃ� (T1�̂�)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int AddByHash(int hashCode, T1 data)
        {
            // T2�ɂ̓f�t�H���g�l��ݒ�
            return this.AddByHash(hashCode, data, default);
        }

        /// <summary>
        /// �n�b�V���R�[�h/�C���X�^���XID�ƃf�[�^��ǉ��܂��͍X�V���A�l�̃C���f�b�N�X��Ԃ� (T1��T2)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int AddByHash(int hashCode, T1 data1, T2 data2)
        {
            // ���׌W���`�F�b�N - �K�v�ɉ����ă��T�C�Y
            if ( (this._count - this._freeCount) >= this._entries.Length * LOAD_FACTOR )
            {
                this.Resize(this._entries.Length * 2);
            }

            // �o�P�b�g�C���f�b�N�X���v�Z�i�P�����W�����@�j
            int bucketIndex = this.GetBucketIndex(hashCode);

            // �����L�[�����ɑ��݂��邩����
            int entryIndex = this._buckets[bucketIndex];
            while ( entryIndex != -1 )
            {
                ref Entry entry = ref this._entries.ElementAt(entryIndex);
                // �n�b�V���R�[�h�݂̂Ń`�F�b�N�iGetInstanceID()�Ɠ����l�Ȃ̂ň�Ӑ����ۏ؂����j
                if ( entry.HashCode == hashCode )
                {
                    // �����G���g�����X�V
                    this._values1[entry.ValueIndex] = data1;
                    this._values2[entry.ValueIndex] = data2;
                    return entry.ValueIndex;
                }

                // �n�b�V���R�[�h���d�����Ȃ����A�����o�P�b�g�ɗv�f������ꍇ�̓o�P�b�g�Ȃ��̎��̗v�f�Ƃ��ĕۑ��B
                entryIndex = entry.NextInBucket;
            }

            // �V�����G���g���p�̃C���f�b�N�X���m��
            int newIndex;
            if ( this._freeCount > 0 )
            {
                // �폜�ς݃G���g�����ė��p
                newIndex = this._freeListHead;
                this._freeListHead = this._entries.ElementAt(newIndex).NextInBucket;
                this._freeCount--;
            }
            else
            {
                // �V�����X���b�g���g�p
                if ( this._count == this._entries.Length )
                {
                    this.Resize(this._entries.Length * 2);
                    bucketIndex = this.GetBucketIndex(hashCode);
                }

                newIndex = this._count;
                this._count++;
            }

            // �G���g���ƒl�̃��X�g���\���ȑ傫���ɂȂ�悤�g��
            this.EnsureCapacity(newIndex);

            // �V�����G���g���̐ݒ�
            Entry newEntry;
            newEntry.HashCode = hashCode;
            newEntry.ValueIndex = newIndex;
            newEntry.NextInBucket = this._buckets[bucketIndex];
            newEntry.IsOccupied = true;

            // UnsafeList�̍X�V
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

            // T2��managed�^�̔z��Ȃ̂ŒP���ɑ��
            this._values2[newIndex] = data2;

            this._buckets[bucketIndex] = newIndex;

            return newIndex;
        }

        /// <summary>
        /// ����̃C���f�b�N�X�ɑΉ����郊�X�g�̗e�ʂ��m��
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void EnsureCapacity(int index)
        {
            // �K�v�ɉ����Ċe���X�g�̗e�ʂ��g��
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
        /// �w�肵���C���f�b�N�X���L���i�g�p���j���ǂ������m�F
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsValidIndex(int index)
        {
            return index >= 0 && index < this._count && this._entries[index].IsOccupied;
        }

        /// <summary>
        /// �C���f�b�N�X���璼�ڃf�[�^T1���擾�i�Q�Ƃ�Ԃ��j
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
        /// �C���f�b�N�X���璼�ڃf�[�^T2���擾
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
        /// �C���f�b�N�X���璼�ڃf�[�^T2��ݒ�
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
        /// �Q�[���I�u�W�F�N�g����f�[�^T1, T2�Ɠ����C���f�b�N�X���擾
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

            // GetHashCode()���g�p�iGetInstanceID()�Ɠ����l�j
            return this.TryGetValueByHash(obj.GetHashCode(), out data1, out data2, out index);
        }

        /// <summary>
        /// �Q�[���I�u�W�F�N�g����f�[�^T1�Ɠ����C���f�b�N�X���擾
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

            // GetHashCode()���g�p�iGetInstanceID()�Ɠ����l�j
            return this.TryGetValueByHash(obj.GetHashCode(), out data1, out index);
        }

        /// <summary>
        /// �Q�[���I�u�W�F�N�g����f�[�^T2�Ɠ����C���f�b�N�X���擾
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

            // GetHashCode()���g�p�iGetInstanceID()�Ɠ����l�j
            return this.TryGetValueByHash(obj.GetHashCode(), out data2, out index);
        }

        /// <summary>
        /// �n�b�V���R�[�h/�C���X�^���XID����f�[�^T1, T2�Ɠ����C���f�b�N�X���擾
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
        /// �n�b�V���R�[�h/�C���X�^���XID����f�[�^T1�Ɠ����C���f�b�N�X���擾
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
        /// �n�b�V���R�[�h/�C���X�^���XID����T2�Ɠ����C���f�b�N�X���擾
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
        /// �Q�[���I�u�W�F�N�g�Ɋ֘A�t����ꂽ�f�[�^���폜
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Remove(GameObject obj)
        {
            if ( obj == null )
            {
                return false;
            }

            // GetHashCode()���g�p�iGetInstanceID()�Ɠ����l�j
            return this.RemoveByHash(obj.GetHashCode());
        }

        /// <summary>
        /// �n�b�V���R�[�h/�C���X�^���XID�Ɋ֘A�t����ꂽ�f�[�^���폜
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
                    // �G���g���̗v�f��_���폜����B
                    this._values1[entry.ValueIndex].LogicalDelete();
                    this._values2[entry.ValueIndex] = null;

                    // �G���g�����o�P�b�g���X�g����폜
                    if ( prevIndex != -1 )
                    {
                        // �O�̃G���g���̎��̃����N���X�V
                        Entry prevEntry = this._entries[prevIndex];
                        prevEntry.NextInBucket = entry.NextInBucket;
                        this._entries[prevIndex] = prevEntry;
                    }
                    else
                    {
                        this._buckets[bucketIndex] = entry.NextInBucket;
                    }

                    // �G���g����_���I�ɍ폜���ăt���[���X�g�ɒǉ�
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
        /// ���ׂẴG���g�����N���A
        /// </summary>
        public void Clear()
        {
            if ( this._count == 0 )
            {
                return;
            }

            // �o�P�b�g��-1�ŏ�����
            for ( int i = 0; i < this._buckets.Length; i++ )
            {
                this._buckets[i] = -1;
            }

            // UnsafeList���N���A
            this._entries.Clear();
            this._values1.Clear();

            this._count = 0;
            this._freeCount = 0;
            this._freeListHead = -1;
        }

        #region �����f�[�^�Ǘ�����

        /// <summary>
        /// �����z��̃T�C�Y��ύX
        /// </summary>
        private void Resize(int newCapacity)
        {
            // �f���T�C�Y�ɒ���
            int newPrimeSize = this.GetNextPrimeSize(newCapacity);

            // �e�R���N�V�����̃T�C�Y���g���i�����f�[�^��ێ��j
            this._entries.Resize(newPrimeSize, NativeArrayOptions.ClearMemory);
            this._values1.Resize(newPrimeSize, NativeArrayOptions.ClearMemory);

            // managed�^�z��̃��T�C�Y
            if ( this._values2.Length < newPrimeSize )
            {
                Array.Resize(ref this._values2, newPrimeSize);
            }

            // �o�P�b�g���X�g��V�����T�C�Y�ō�蒼���A-1�ŏ�����
            this._buckets.Resize(newPrimeSize, NativeArrayOptions.ClearMemory);

            for ( int i = 0; i < newPrimeSize; i++ )
            {
                this._buckets[i] = -1;
            }

            // ���ׂẴG���g�����ăn�b�V��
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
        /// �n�b�V���R�[�h����o�P�b�g�C���f�b�N�X���擾�i�P�����W�����@�j
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int GetBucketIndex(int hashCode)
        {
            // �n�b�V���R�[�h�͕��̒l�����蓾��̂ŁA��Βl�������Ɏ擾
            // hashCode & 0x7FFFFFFF �� Math.Abs(hashCode) ��荂��
            // ��Βl��Mod���Ƃ邱�Ƃ� 0�`�v�f��-1�A�̊Ԃ̃C���f�b�N�X���擾�ł���
            return (hashCode & 0x7FFFFFFF) % this._buckets.Length;
        }

        /// <summary>
        /// �w��T�C�Y�ȏ�̍ŏ��̑f�����擾
        /// </summary>
        private int GetNextPrimeSize(int minSize)
        {
            // �o�C�i���T�[�`�őf���e�[�u������K�؂Ȓl��T��
            int index = Array.BinarySearch(PrimeSizes, minSize);

            if ( index >= 0 )
            {
                // �҂������v����f������������
                return PrimeSizes[index];
            }
            else
            {
                // ��v����l���Ȃ��ꍇ�A~index �͑}�����ׂ��ʒu��\��
                int insertIndex = ~index;

                if ( insertIndex < PrimeSizes.Length )
                {
                    // �e�[�u�����ɂ���v�������l���傫���f����Ԃ�
                    return PrimeSizes[insertIndex];
                }
                else
                {
                    // �e�[�u�����̍ő�l���傫���ꍇ�͌v�Z����
                    return this.CalculateNextPrime(minSize);
                }
            }
        }

        /// <summary>
        /// �w��l�ȏ�̎��̑f�����v�Z
        /// </summary>
        private int CalculateNextPrime(int minSize)
        {
            // �����J�n�i������2�ȊO�f���ɂȂ�Ȃ��j
            int candidate = minSize;
            if ( candidate % 2 == 0 )
            {
                candidate++;
            }

            while ( !this.IsPrime(candidate) )
            {
                candidate += 2; // ��݂̂��`�F�b�N
            }

            return candidate;
        }

        /// <summary>
        /// �f������
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

            // 6k�}1�̌`�ŕ\����鐔�̂݃`�F�b�N�i�������j
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

        #endregion �����f�[�^�Ǘ�����

        /// <summary>
        /// �w�肵���Q�[���I�u�W�F�N�g�̃L�[���f�B�N�V���i���ɑ��݂��邩�ǂ������m�F
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
        /// �w�肵���n�b�V���R�[�h/�C���X�^���XID���f�B�N�V���i���ɑ��݂��邩�ǂ������m�F
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
        /// ���ׂĂ̗L���ȃG���g���ɑ΂��ď��������s (T1�̂�)�B<br></br>
        /// IEnumerable�̑���
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
        /// ���ׂĂ̗L���ȃG���g���ɑ΂��ď��������s (T1��T2)�B<br></br>
        /// IEnumerable�̑���
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
        /// �W���u�V�X�e���ŃL�����N�^�[�f�[�^T1���g�p���邽�߂Ƀ��X�g���������擾����B<br></br>
        /// ��΂ɂ����Ŏ󂯎�������X�g��Dispose���Ă͂Ȃ�Ȃ��B���̎���Dictionary�̓Q�[���I�����ɔj������B<br></br>
        /// �܂��A�Ӑ}�����Q�Ƃ��c��Ȃ��悤�Ƀ��[�J���ϐ��ȊO�Ŏ󂯎���Ă����߁B<br></br>
        /// ReadOnly�ɂ������Ƃ��낾���ǁA��������Ƃ��낢��g���ɂ�������d���Ȃ��B
        /// </summary>
        /// <returns>T1�f�[�^��UnsafeList</returns>
        public UnsafeList<T1> GetInternalList1ForJob()
        {
            // �������X�g��Ԃ�
            return this._values1;
        }

        /// <summary>
        /// �L�����N�^�[�f�[�^T2���g�p���邽�߂ɓ����z��ւ̎Q�Ƃ��擾����B<br></br>
        /// T2��managed�^�̂��߁AJobSystem�ł͎g�p�ł��Ȃ��B<br></br>
        /// ReadOnly�ɂ������Ƃ��낾���ǁA��������Ƃ��낢��g���ɂ�������d���Ȃ��B
        /// </summary>
        /// <returns>T2�f�[�^��Span</returns>
        public Span<T2> GetInternalArray2()
        {
            // �����z���Ԃ�
            return this._values2.AsSpan();
        }

        /// <summary>
        /// ���\�[�X�����
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
            // _values2��managed�^�̔z��Ȃ̂ŎQ�Ƃ����؂�B
            this._values2 = null;

            this._isDisposed = true;
        }
    }
}
