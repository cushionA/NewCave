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
    /// �L�����N�^�[�f�[�^�̎�ނ�\���񋓌^
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
    /// �Q�[���I�u�W�F�N�g��GetHashCode()���L�[�Ƃ��ĊǗ�����L�����N�^�[�f�[�^����
    /// Unity�ł�GetHashCode()��GetInstanceID()�Ɠ����l��Ԃ����߁A�L�[�̈�Ӑ����ۏ؂���Ă���B
    /// �����ł̃f�[�^�ێ���NativeList���g�p����GC���ׂ��팸
    /// SoA�p�^�[���ŃL�����N�^�[�̊e��f�[�^�������I�ɊǗ�
    /// </summary>
    public class SoACharaDataDic : IDisposable
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
        private NativeList<int> _buckets;

        /// <summary>
        /// �G���g���̃��X�g
        /// </summary>
        private NativeList<Entry> _entries;

        #region �Ǘ��Ώۂ̃f�[�^�BSoA�ɏ]���L�����̐��������B

        /// <summary>
        /// �L�����N�^�[�̊�{���iHP�AMP�A�ʒu�j
        /// </summary>
        private NativeList<CharacterBaseInfo> _characterBaseInfo;

        /// <summary>
        /// �U���͂̃f�[�^
        /// </summary>
        private NativeList<CharacterAtkStatus> _characterAtkStatus;

        /// <summary>
        /// �h��͂̃f�[�^
        /// </summary>
        private NativeList<CharacterDefStatus> _characterDefStatus;

        /// <summary>
        /// AI���Q�Ƃ��邽�߂̃L�����N�^�[�̏�ԏ��
        /// </summary>
        private NativeList<CharacterStateInfo> _characterStateInfo;

        /// <summary>
        /// �L�����̍s���i���s���x�Ƃ��j�̃X�e�[�^�X�B
        /// </summary>
        private NativeList<MoveStatus> _moveStatus;

        /// <summary>
        /// �Q�ƕp�x�����Ȃ��A�����ĘA���Q�Ƃ���Ȃ��f�[�^���W�߂��\���́B
        /// </summary>
        private NativeList<CharaColdLog> _coldLog;

        /// <summary>
        /// BaseController���i�[����z��imanaged�^�̂��ߒʏ�̔z��j
        /// </summary>
        private BaseController[] _controllers;

        #endregion

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

        /// <summary>
        /// �R���X�g���N�^
        /// </summary>
        /// <param name="capacity">�����e�ʁi�f���ɒ�������܂��j</param>
        /// <param name="allocator">�������A���P�[�^�i�f�t�H���g��Persistent�j</param>
        public SoACharaDataDic(int capacity = DEFAULT_CAPACITY, Allocator allocator = Allocator.Persistent)
        {
            // �A���P�[�^�ۑ�
            this._allocator = allocator;

            // �w��e�ʈȏ�̍ŏ��̑f����I��
            int primeCapacity = this.GetNextPrimeSize(capacity);

            // NativeList�̏�����
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

            // �o�P�b�g���X�g�̗e�ʊm�ۂ�-1�ŏ�����
            this._buckets.Resize(primeCapacity, NativeArrayOptions.ClearMemory);
            for ( int i = 0; i < this._buckets.Length; i++ )
            {
                this._buckets[i] = -1;
            }

            // �G���g�����X�g�̗e�ʂ��m��
            this._entries.Capacity = primeCapacity;

            this._freeListHead = -1;
            this._count = 0;
            this._freeCount = 0;
        }

        /// <summary>
        /// �Q�[���I�u�W�F�N�g�ƑS�L�����N�^�[�f�[�^��ǉ��܂��͍X�V
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
        /// �n�b�V���R�[�h�ƑS�L�����N�^�[�f�[�^��ǉ��܂��͍X�V���A�l�̃C���f�b�N�X��Ԃ�
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int AddByHash(int hashCode, CharacterBaseInfo baseInfo, CharacterAtkStatus atkStatus,
                            CharacterDefStatus defStatus, CharacterStateInfo stateInfo,
                            MoveStatus moveStatus, CharaColdLog coldLog, BaseController controller)
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
                if ( entry.HashCode == hashCode )
                {
                    // �����G���g�����X�V
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

            // NativeList�ƃR���g���[���[�z��̍X�V
            if ( newIndex < this._entries.Length )
            {
                this._entries[newIndex] = newEntry;
            }
            else
            {
                this._entries.Add(newEntry);
            }

            // �e�f�[�^���X�g�Ƀf�[�^��ǉ�
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

            // �R���g���[���[��managed�^�̔z��Ȃ̂ŒP���ɑ��
            this._controllers[newIndex] = controller;

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
        /// �w�肵���C���f�b�N�X���L���i�g�p���j���ǂ������m�F
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsValidIndex(int index)
        {
            return index >= 0 && index < this._count && this._entries[index].IsOccupied;
        }

        /// <summary>
        /// �C���f�b�N�X���璼�ڎw�肳�ꂽ�f�[�^�^�C�v�̃f�[�^���擾
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
        /// �C���f�b�N�X���璼��BaseController���擾
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
        /// �C���f�b�N�X���璼��BaseController��ݒ�
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
        /// �Q�[���I�u�W�F�N�g����S�L�����N�^�[�f�[�^�Ɠ����C���f�b�N�X���擾
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
        /// �n�b�V���R�[�h����S�L�����N�^�[�f�[�^�Ɠ����C���f�b�N�X���擾
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
        /// �Q�[���I�u�W�F�N�g�Ɋ֘A�t����ꂽ�f�[�^���폜
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
        /// �n�b�V���R�[�h�Ɋ֘A�t����ꂽ�f�[�^���폜
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
                    // �e�f�[�^��_���폜�iILogicalDelate�C���^�[�t�F�[�X���������Ă���ꍇ�j
                    // this._characterBaseInfo[entry.ValueIndex].LogicalDelete();
                    // ���̃f�[�^�����Z�b�g
                    this._characterBaseInfo[entry.ValueIndex] = default;
                    this._characterAtkStatus[entry.ValueIndex] = default;
                    this._characterDefStatus[entry.ValueIndex] = default;
                    this._characterStateInfo[entry.ValueIndex] = default;
                    this._moveStatus[entry.ValueIndex] = default;
                    this._coldLog[entry.ValueIndex] = default;
                    this._controllers[entry.ValueIndex] = null;

                    // �G���g�����o�P�b�g���X�g����폜
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

            // NativeList���N���A
            this._entries.Clear();
            this._characterBaseInfo.Clear();
            this._characterAtkStatus.Clear();
            this._characterDefStatus.Clear();
            this._characterStateInfo.Clear();
            this._moveStatus.Clear();
            this._coldLog.Clear();

            // �R���g���[���[�z����N���A
            Array.Clear(this._controllers, 0, this._controllers.Length);

            this._count = 0;
            this._freeCount = 0;
            this._freeListHead = -1;
        }

        #region �����f�[�^�擾�v���p�e�B

        #region �����f�[�^�擾�v���p�e�B - CharacterBaseInfo

        /// <summary>
        /// GameObject����CharacterBaseInfo���擾�i�Q�Ƃ�Ԃ��j
        /// �ł��ėp�I�����A�n�b�V���v�Z�ƃo�P�b�g�������������邽�ߒ����x�̑��x
        /// </summary>
        /// <param name="obj">�Ώۂ�GameObject</param>
        /// <returns>CharacterBaseInfo�̎Q��</returns>
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
        /// �n�b�V���l����CharacterBaseInfo���擾�i�Q�Ƃ�Ԃ��j
        /// �o�P�b�g�������������邽�ߒ����x�̑��x�AGameObject���͂�⍂��
        /// </summary>
        /// <param name="hashCode">GameObject�̃n�b�V���l</param>
        /// <returns>CharacterBaseInfo�̎Q��</returns>
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
        /// �C���f�b�N�X����CharacterBaseInfo�𒼐ڎ擾�i�Q�Ƃ�Ԃ��j
        /// �ō����x�A�z��ւ̒��ڃA�N�Z�X�̂���
        /// </summary>
        /// <param name="index">�����z��̃C���f�b�N�X</param>
        /// <returns>CharacterBaseInfo�̎Q��</returns>
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

        #region �����f�[�^�擾�v���p�e�B - CharacterAtkStatus

        /// <summary>
        /// GameObject����CharacterAtkStatus���擾�i�Q�Ƃ�Ԃ��j
        /// �U���̓f�[�^�ւ̍ł��ėp�I�ȃA�N�Z�X���@
        /// </summary>
        /// <param name="obj">�Ώۂ�GameObject</param>
        /// <returns>CharacterAtkStatus�̎Q��</returns>
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
        /// �n�b�V���l����CharacterAtkStatus���擾�i�Q�Ƃ�Ԃ��j
        /// �U���̓f�[�^�ւ̒����x���x�ł̃A�N�Z�X
        /// </summary>
        /// <param name="hashCode">GameObject�̃n�b�V���l</param>
        /// <returns>CharacterAtkStatus�̎Q��</returns>
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
        /// �C���f�b�N�X����CharacterAtkStatus�𒼐ڎ擾�i�Q�Ƃ�Ԃ��j
        /// �U���̓f�[�^�ւ̍ō����x�A�N�Z�X�AJob���ł̎g�p�ɍœK
        /// </summary>
        /// <param name="index">�����z��̃C���f�b�N�X</param>
        /// <returns>CharacterAtkStatus�̎Q��</returns>
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

        #region �����f�[�^�擾�v���p�e�B - CharacterDefStatus

        /// <summary>
        /// GameObject����CharacterDefStatus���擾�i�Q�Ƃ�Ԃ��j
        /// �h��̓f�[�^�ւ̔ėp�I�ȃA�N�Z�X�AUI��X�N���v�g����̎Q�ƂɓK���Ă���
        /// </summary>
        /// <param name="obj">�Ώۂ�GameObject</param>
        /// <returns>CharacterDefStatus�̎Q��</returns>
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
        /// �n�b�V���l����CharacterDefStatus���擾�i�Q�Ƃ�Ԃ��j
        /// �h��̓f�[�^�ւ̌����I�ȃA�N�Z�X���@
        /// </summary>
        /// <param name="hashCode">GameObject�̃n�b�V���l</param>
        /// <returns>CharacterDefStatus�̎Q��</returns>
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
        /// �C���f�b�N�X����CharacterDefStatus�𒼐ڎ擾�i�Q�Ƃ�Ԃ��j
        /// �h��̓f�[�^�ւ̍ō����x�A�N�Z�X�A�_���[�W�v�Z�����Ȃǂŏd�v
        /// </summary>
        /// <param name="index">�����z��̃C���f�b�N�X</param>
        /// <returns>CharacterDefStatus�̎Q��</returns>
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

        #region �����f�[�^�擾�v���p�e�B - CharacterStateInfo

        /// <summary>
        /// GameObject����CharacterStateInfo���擾�i�Q�Ƃ�Ԃ��j
        /// AI�̏�ԏ��ւ̔ėp�I�ȃA�N�Z�X�A�f�o�b�O��UI�\���Ŏg�p
        /// </summary>
        /// <param name="obj">�Ώۂ�GameObject</param>
        /// <returns>CharacterStateInfo�̎Q��</returns>
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
        /// �n�b�V���l����CharacterStateInfo���擾�i�Q�Ƃ�Ԃ��j
        /// AI��ԏ��ւ̌����I�ȃA�N�Z�X�AAI�}�l�[�W���[����Ăяo�����
        /// </summary>
        /// <param name="hashCode">GameObject�̃n�b�V���l</param>
        /// <returns>CharacterStateInfo�̎Q��</returns>
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
        /// �C���f�b�N�X����CharacterStateInfo�𒼐ڎ擾�i�Q�Ƃ�Ԃ��j
        /// AI��ԏ��ւ̍ō����x�A�N�Z�X�AAI���fJob�ł̎g�p�ɍœK��
        /// </summary>
        /// <param name="index">�����z��̃C���f�b�N�X</param>
        /// <returns>CharacterStateInfo�̎Q��</returns>
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

        #region �����f�[�^�擾�v���p�e�B - MoveStatus

        /// <summary>
        /// GameObject����MoveStatus���擾�i�Q�Ƃ�Ԃ��j
        /// �ړ��֘A�X�e�[�^�X�ւ̔ėp�I�ȃA�N�Z�X�A�ړ����x�̎Q�ƂȂǂŎg�p
        /// </summary>
        /// <param name="obj">�Ώۂ�GameObject</param>
        /// <returns>MoveStatus�̎Q��</returns>
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
        /// �n�b�V���l����MoveStatus���擾�i�Q�Ƃ�Ԃ��j
        /// �ړ��X�e�[�^�X�ւ̌����I�ȃA�N�Z�X���@
        /// </summary>
        /// <param name="hashCode">GameObject�̃n�b�V���l</param>
        /// <returns>MoveStatus�̎Q��</returns>
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
        /// �C���f�b�N�X����MoveStatus�𒼐ڎ擾�i�Q�Ƃ�Ԃ��j
        /// �ړ��X�e�[�^�X�ւ̍ō����x�A�N�Z�X�A�ړ��nJob�ł̃p�t�H�[�}���X�d��
        /// </summary>
        /// <param name="index">�����z��̃C���f�b�N�X</param>
        /// <returns>MoveStatus�̎Q��</returns>
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

        #region �����f�[�^�擾�v���p�e�B - CharaColdLog

        /// <summary>
        /// GameObject����CharaColdLog���擾�i�Q�Ƃ�Ԃ��j
        /// ��p�x�A�N�Z�X�f�[�^�ւ̔ėp�I�Ȏ擾���@�A���v���̎Q�ƂȂǂŎg�p
        /// </summary>
        /// <param name="obj">�Ώۂ�GameObject</param>
        /// <returns>CharaColdLog�̎Q��</returns>
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
        /// �n�b�V���l����CharaColdLog���擾�i�Q�Ƃ�Ԃ��j
        /// ��p�x�f�[�^�ւ̌����I�ȃA�N�Z�X�A�o�b�N�O���E���h�����Ŏg�p
        /// </summary>
        /// <param name="hashCode">GameObject�̃n�b�V���l</param>
        /// <returns>CharaColdLog�̎Q��</returns>
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
        /// �C���f�b�N�X����CharaColdLog�𒼐ڎ擾�i�Q�Ƃ�Ԃ��j
        /// ��p�x�f�[�^�ւ̒��ڃA�N�Z�X�A�����e�i���XJob�ȂǂŎg�p
        /// </summary>
        /// <param name="index">�����z��̃C���f�b�N�X</param>
        /// <returns>CharaColdLog�̎Q��</returns>
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

        #region �����f�[�^�擾�v���p�e�B - BaseController

        /// <summary>
        /// GameObject����BaseController���擾�i�l��Ԃ��j
        /// �L�����N�^�[�R���g���[���[�ւ̔ėp�I�ȃA�N�Z�X�AUI�X�V�ȂǂŎg�p
        /// managed�^�̂��ߎQ�Ƃł͂Ȃ��l��Ԃ�
        /// </summary>
        /// <param name="obj">�Ώۂ�GameObject</param>
        /// <returns>BaseController�̃C���X�^���X</returns>
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
        /// �n�b�V���l����BaseController���擾�i�l��Ԃ��j
        /// �R���g���[���[�ւ̌����I�ȃA�N�Z�X���@
        /// </summary>
        /// <param name="hashCode">GameObject�̃n�b�V���l</param>
        /// <returns>BaseController�̃C���X�^���X</returns>
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
        /// �C���f�b�N�X����BaseController�𒼐ڎ擾�i�l��Ԃ��j
        /// �R���g���[���[�ւ̍ō����x�A�N�Z�X�A������managed�^�̂���JobSystem�ł͎g�p�s��
        /// </summary>
        /// <param name="index">�����z��̃C���f�b�N�X</param>
        /// <returns>BaseController�̃C���X�^���X</returns>
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
        /// �C���f�b�N�X��BaseController��ݒ�i�l��ݒ�j
        /// �R���g���[���[�̒��ڐݒ�A�������⃊�Z�b�g���Ɏg�p
        /// </summary>
        /// <param name="index">�����z��̃C���f�b�N�X</param>
        /// <param name="controller">�ݒ肷��BaseController�̃C���X�^���X</param>
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
            this._characterBaseInfo.Resize(newPrimeSize, NativeArrayOptions.ClearMemory);
            this._characterAtkStatus.Resize(newPrimeSize, NativeArrayOptions.ClearMemory);
            this._characterDefStatus.Resize(newPrimeSize, NativeArrayOptions.ClearMemory);
            this._characterStateInfo.Resize(newPrimeSize, NativeArrayOptions.ClearMemory);
            this._moveStatus.Resize(newPrimeSize, NativeArrayOptions.ClearMemory);
            this._coldLog.Resize(newPrimeSize, NativeArrayOptions.ClearMemory);

            // �R���g���[���[�z��̃��T�C�Y
            if ( this._controllers.Length < newPrimeSize )
            {
                Array.Resize(ref this._controllers, newPrimeSize);
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
            return (hashCode & 0x7FFFFFFF) % this._buckets.Length;
        }

        /// <summary>
        /// �w��T�C�Y�ȏ�̍ŏ��̑f�����擾
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
        /// �w��l�ȏ�̎��̑f�����v�Z
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
        /// �w�肵���n�b�V���R�[�h���f�B�N�V���i���ɑ��݂��邩�ǂ������m�F
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
        /// ���ׂĂ̗L���ȃG���g���ɑ΂��ď��������s
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
            this._characterBaseInfo.Dispose();
            this._characterAtkStatus.Dispose();
            this._characterDefStatus.Dispose();
            this._characterStateInfo.Dispose();
            this._moveStatus.Dispose();
            this._coldLog.Dispose();

            // �R���g���[���[�z��͎Q�Ƃ����؂�
            this._controllers = null;

            this._isDisposed = true;
        }
    }
}