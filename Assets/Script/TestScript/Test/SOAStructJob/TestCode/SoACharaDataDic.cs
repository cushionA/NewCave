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
    /// �Œ�T�C�Y�E�X���b�v�폜�ł̃L�����N�^�[�f�[�^����
    /// �ő�e�ʂ����O�Ɋm�ۂ����T�C�Y���Ȃ�
    /// �폜���͍폜�����ƍ��̍Ō�̗v�f�����ւ��邱�ƂŃf�[�^���f�Љ����Ȃ�
    /// �n�b�V���e�[�u���ɂ��GetComponent�s�v�Ńf�[�^�A�N�Z�X���\
    /// </summary>
    public unsafe class SoACharaDataDic : IDisposable
    {
        #region ��`

        /// <summary>
        /// �G���g���\���� - �n�b�V���e�[�u���̃G���g�����
        /// </summary>
        private struct Entry
        {
            /// <summary>
            /// �Q�[���I�u�W�F�N�g�̃n�b�V���R�[�h�iGetInstanceID()�Ɠ���j
            /// </summary>
            public int HashCode;

            /// <summary>
            /// �f�[�^�z����̃C���f�b�N�X
            /// </summary>
            public int ValueIndex;

            /// <summary>
            /// �����o�P�b�g���̎��̃G���g���ւ̃C���f�b�N�X�i-1�͏I�[�j
            /// </summary>
            public int NextInBucket;
        }

        /// <summary>
        /// ���������C�A�E�g���
        /// �ꊇ�Ŋm�ۂ������������ŁA���ꂼ��̃f�[�^�̃������z�u���ǂ�����n�܂邩���L�^����B
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

        #region �萔

        /// <summary>
        /// �f�t�H���g�̍ő�e��
        /// </summary>
        private const int DEFAULT_MAX_CAPACITY = 130;

        /// <summary>
        /// �o�P�b�g���i�n�b�V���e�[�u���̃T�C�Y�j
        /// </summary>
        private const int BUCKET_COUNT = 191;  // �f�����g�p

        #endregion

        #region �t�B�[���h

        /// <summary>
        /// �o�P�b�g�z��i�e�v�f�̓G���g���ւ̃C���f�b�N�X�A-1�͋�j
        /// </summary>
        private int[] _buckets;

        /// <summary>
        /// �G���g���̃��X�g�i�n�b�V�����C���f�b�N�X�̃}�b�s���O�j
        /// </summary>
        private UnsafeList<Entry> _entries;

        /// <summary>
        /// �n�b�V���R�[�h������ۂ̃f�[�^�C���f�b�N�X�ւ̃}�b�s���O
        /// �X���b�v�폜���ɍX�V���K�v
        /// </summary>
        private int[] _dataIndexToHash;

        /// <summary>
        /// �G���g���폜��̎g���܂킹��X�y�[�X�̃C���f�b�N�X�B
        /// </summary>
        private Stack<int> _freeEntry;

        #region �Ǘ��Ώۂ̃f�[�^

        /// <summary>
        /// �ꊇ�m�ۂ����������u���b�N
        /// </summary>
        private byte* _bulkMemory;
        private int _totalMemorySize;

        /// <summary>
        /// �L�����N�^�[�̊�{���
        /// </summary>
        public UnsafeList<CharacterBaseInfo> _characterBaseInfo;

        /// <summary>
        /// �L�����N�^�[�̊�{���
        /// </summary>
        public UnsafeList<SolidData> _solidData;

        /// <summary>
        /// �U���͂̃f�[�^
        /// </summary>
        public UnsafeList<CharacterAtkStatus> _characterAtkStatus;

        /// <summary>
        /// �h��͂̃f�[�^
        /// </summary>
        public UnsafeList<CharacterDefStatus> _characterDefStatus;

        /// <summary>
        /// AI���Q�Ƃ��邽�߂̏�ԏ��
        /// </summary>
        public UnsafeList<CharacterStateInfo> _characterStateInfo;

        /// <summary>
        /// �ړ��֘A�̃X�e�[�^�X
        /// </summary>
        public UnsafeList<MoveStatus> _moveStatus;

        /// <summary>
        /// �Q�ƕp�x�̒Ⴂ�f�[�^
        /// </summary>
        public UnsafeList<CharaColdLog> _coldLog;

        /// <summary>
        /// BaseController���i�[����z��
        /// </summary>
        private BaseController[] _controllers;

        #endregion

        /// <summary>
        /// ���݂̗v�f��
        /// </summary>
        private int _count;

        /// <summary>
        /// �ő�e��
        /// </summary>
        private readonly int _maxCapacity;

        /// <summary>
        /// �������A���P�[�^
        /// </summary>
        private readonly Allocator _allocator;

        /// <summary>
        /// ����ς݃t���O
        /// </summary>
        private bool _isDisposed;

        #endregion

        #region �v���p�e�B

        /// <summary>
        /// ���݂̗v�f��
        /// </summary>
        public int Count => this._count;

        /// <summary>
        /// �ő�e��
        /// </summary>
        public int MaxCapacity => this._maxCapacity;

        /// <summary>
        /// �g�p���i0.0�`1.0�j
        /// </summary>
        public float UsageRatio => (float)this._count / this._maxCapacity;

        /// <summary>
        /// ���̒����̃L�����N�^�[�R���g���[���[��Ԃ��B
        /// </summary>
        public Span<BaseController> GetController => this._controllers.AsSpan().Slice(0, this._count);

        #endregion

        #region �R���X�g���N�^

        /// <summary>
        /// �R���X�g���N�^
        /// </summary>
        /// <param name="maxCapacity">�ő�e�ʁi�f�t�H���g: 100�j</param>
        /// <param name="allocator">�������A���P�[�^�i�f�t�H���g: Persistent�j</param>
        public SoACharaDataDic(int maxCapacity = DEFAULT_MAX_CAPACITY, Allocator allocator = Allocator.Persistent)
        {
            this._maxCapacity = maxCapacity;
            this._allocator = allocator;
            this._count = 0;
            this._isDisposed = false;

            // �o�P�b�g�z��̏�����
            this._buckets = new int[BUCKET_COUNT];
            this._buckets.AsSpan().Fill(-1);

            // �G���g�����X�g�̏�����
            this._entries = new UnsafeList<Entry>(BUCKET_COUNT * 2, allocator);

            // �폜�G���g���ۊǗp�̃G���g�����X�g���쐬�B
            this._freeEntry = new Stack<int>(maxCapacity);

            // �n�b�V�����f�[�^�C���f�b�N�X�̃}�b�s���O
            this._dataIndexToHash = new int[maxCapacity];
            this._dataIndexToHash.AsSpan().Fill(-1);

            // ���������C�A�E�g�̌v�Z
            MemoryLayout layout = this.CalculateMemoryLayout(maxCapacity);
            this._totalMemorySize = layout.TotalSize;

            // �ꊇ�������m��
            this._bulkMemory = (byte*)UnsafeUtility.Malloc(this._totalMemorySize, 64, allocator);
            UnsafeUtility.MemClear(this._bulkMemory, this._totalMemorySize);

            // �eUnsafeList�̏������i�Œ�T�C�Y�j
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

            // BaseController�z��
            this._controllers = new BaseController[maxCapacity];

            // ������������
            this._characterBaseInfo.Length = 0;
            this._characterAtkStatus.Length = 0;
            this._characterDefStatus.Length = 0;
            this._solidData.Length = 0;
            this._characterStateInfo.Length = 0;
            this._moveStatus.Length = 0;
            this._coldLog.Length = 0;
        }

        #endregion

        #region �ǉ��E�X�V

        /// <summary>
        /// �Q�[���I�u�W�F�N�g�ƑS�L�����N�^�[�f�[�^��ǉ��܂��͍X�V
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
        /// �Q�[���I�u�W�F�N�g�ƑS�L�����N�^�[�f�[�^��ǉ��܂��͍X�V
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
        /// �n�b�V���R�[�h�Ńf�[�^��ǉ��܂��͍X�V
        /// </summary>
        public int AddByHash(int hashCode, CharacterBaseInfo baseInfo, CharacterAtkStatus atkStatus,
                            CharacterDefStatus defStatus, SolidData solidData, CharacterStateInfo stateInfo,
                            MoveStatus moveStatus, CharaColdLog coldLog, BaseController controller)
        {

            // �����G���g���̌���
            if ( this.TryGetIndexByHash(hashCode, out int existingIndex) )
            {
                // �X�V
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

            // �e�ʃ`�F�b�N
            if ( this._count >= this._maxCapacity )
            {
                throw new InvalidOperationException($"Maximum capacity ({this._maxCapacity}) exceeded");
            }

            // �V�K�ǉ�
            int dataIndex = this._count;

            // �f�[�^��ǉ�
            this._characterBaseInfo.AddNoResize(baseInfo);
            this._characterAtkStatus.AddNoResize(atkStatus);
            this._characterDefStatus.AddNoResize(defStatus);
            this._solidData.AddNoResize(solidData);
            this._characterStateInfo.AddNoResize(stateInfo);
            this._moveStatus.AddNoResize(moveStatus);
            this._coldLog.AddNoResize(coldLog);
            this._controllers[dataIndex] = controller;

            // �n�b�V���e�[�u���ւ̓o�^
            int bucketIndex = this.GetBucketIndex(hashCode);

            // �G���g���̒ǉ�
            int newEntryIndex;

            // �V�����G���g���̓o�P�b�g�̒����ɒǉ������B
            // ������O�̒����̗v�f�� NextInBucket �Ɍq���Ă�B
            Entry newEntry = new()
            {
                HashCode = hashCode,
                ValueIndex = dataIndex,
                NextInBucket = this._buckets[bucketIndex]
            };

            // �t���[���X�g����ė��p or �V�K�ǉ�
            if ( this._freeEntry.TryPop(out newEntryIndex) )
            {
                this._entries[newEntryIndex] = newEntry;
            }
            // �ė��p�ł��Ȃ��ꍇ�͍Ō���ɃG���g���ǉ�
            else
            {
                newEntryIndex = this._entries.Length;
                this._entries.AddNoResize(newEntry);
            }

            // �o�P�b�g�̒����ɐV�G���g����ǉ�
            this._buckets[bucketIndex] = newEntryIndex;

            // �}�b�s���O�̍X�V
            this._dataIndexToHash[dataIndex] = hashCode;

            this._count++;

            return dataIndex;
        }

        #endregion

        #region �폜�i�X���b�v�폜�j

        /// <summary>
        /// �Q�[���I�u�W�F�N�g�Ɋ֘A�t����ꂽ�f�[�^���폜�i�X���b�v�폜�j
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
        /// �n�b�V���R�[�h�Ɋ֘A�t����ꂽ�f�[�^���폜�i�X���b�v�폜�j
        /// O(1)�̍폜���������邽�߁A�폜�Ώۂ��Ō�̗v�f�Ɠ���ւ���
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool RemoveByHash(int hashCode)
        {
            if ( !this.TryGetIndexByHash(hashCode, out int dataIndex) )
            {
                return false;
            }

            int lastIndex = this._count - 1;

            // �폜�Ώۂ��Ō�̗v�f�łȂ��ꍇ�͓���ւ�
            if ( dataIndex != lastIndex )
            {
                // �Ō�̗v�f���폜�ʒu�ɃR�s�[
                this._characterBaseInfo[dataIndex] = this._characterBaseInfo[lastIndex];
                this._characterAtkStatus[dataIndex] = this._characterAtkStatus[lastIndex];
                this._characterDefStatus[dataIndex] = this._characterDefStatus[lastIndex];
                this._solidData[dataIndex] = this._solidData[lastIndex];
                this._characterStateInfo[dataIndex] = this._characterStateInfo[lastIndex];
                this._moveStatus[dataIndex] = this._moveStatus[lastIndex];
                this._coldLog[dataIndex] = this._coldLog[lastIndex];
                this._controllers[dataIndex] = this._controllers[lastIndex];

                // �ړ������v�f�̃n�b�V���R�[�h�������ă}�b�s���O���X�V
                this._dataIndexToHash[dataIndex] = this._dataIndexToHash[lastIndex];

                // �G���g�����̒l�C���f�b�N�X���X�V
                this.UpdateEntryDataIndex(this._dataIndexToHash[lastIndex], dataIndex);

            }

            // ���X�g�̒��������炷
            this._characterBaseInfo.Length--;
            this._characterAtkStatus.Length--;
            this._characterDefStatus.Length--;
            this._solidData.Length--;
            this._characterStateInfo.Length--;
            this._moveStatus.Length--;
            this._coldLog.Length--;

            // �n�b�V���e�[�u������G���g�����폜
            this.RemoveFromHashTable(hashCode);

            this._count--;
            return true;
        }

        /// <summary>
        /// �G���g���̃f�[�^�C���f�b�N�X���X�V�B<br/>
        /// �����f�[�^�̈ʒu�ړ��ɔ����A�G���g�����̒l�C���f�b�N�X�̒l������������B<br/>
        /// </summary>
        /// <param name="hashCode">���������Ώۂ̃n�b�V���l</param>
        /// <param name="newDataIndex">�V�����G���g���Ɋ��蓖�Ă�l�̈ʒu</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void UpdateEntryDataIndex(int hashCode, int newDataIndex)
        {
            // �X�V�Ώۂ̃G���g�����o�P�b�g�̂ǂ̈ʒu�ɂ��邩���v�Z����
            int bucketIndex = this.GetBucketIndex(hashCode);

            // ���݂̃G���g���̊J�n�ʒu���擾����B
            int entryIndex = this._buckets[bucketIndex];

            // ref�Q�ƂŃG���g����T���A�G���g�����̒l�̃C���f�b�N�X������������B
            while ( entryIndex != -1 )
            {
                ref Entry entry = ref this._entries.ElementAt(entryIndex);

                // �n�b�V���l����v����G���g��������Βl�C���f�b�N�X������������B
                if ( entry.HashCode == hashCode )
                {
                    entry.ValueIndex = newDataIndex;
                    return;
                }

                entryIndex = entry.NextInBucket;
            }
        }

        /// <summary>
        /// �n�b�V���e�[�u������G���g�����폜
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void RemoveFromHashTable(int hashCode)
        {
            // �폜�Ώۂ̃G���g�����o�P�b�g�̂ǂ̈ʒu�ɂ��邩���v�Z����
            int bucketIndex = this.GetBucketIndex(hashCode);

            // �폜�Ώۂ̃G���g���̊J�n�ʒu���擾����B
            int entryIndex = this._buckets[bucketIndex];

            // �O�̃G���g�����|�P�̎��̓o�P�b�g�̒����̃G���g���B
            int prevIndex = -1;

            while ( entryIndex != -1 )
            {
                ref Entry entry = ref this._entries.ElementAt(entryIndex);

                // �n�b�V���l����v����G���g��������Βl�C���f�b�N�X������������
                if ( entry.HashCode == hashCode )
                {
                    // �o�P�b�g���̈�ڂ̃G���g�����폜�ΏۂȂ�A�o�P�b�g����̎Q�Ƃ𒼐ڏ����ς���B
                    // [�폜�ΏہA���G���g���A���X�G���g��] �Ƃ����o�P�b�g��[���G���g���A���X�G���g��]�ɂ��� 
                    if ( prevIndex == -1 )
                    {
                        this._buckets[bucketIndex] = entry.NextInBucket;
                    }

                    // �o�P�b�g���őO�̃G���g������Q�Ƃ���Ă���Ȃ�A�O�̃G���g���Ɏ����̎��̃G���g�����q�������B
                    // [�O�G���g���A�폜�ΏہA���G���g��] �Ƃ����o�P�b�g��[�O�G���g���A���G���g��]�ɂ��� 
                    else
                    {
                        ref Entry prevEntry = ref this._entries.ElementAt(prevIndex);
                        prevEntry.NextInBucket = entry.NextInBucket;
                    }

                    // ������ꂽ�C���f�b�N�X���X�^�b�N�ɒǉ�
                    this._freeEntry.Push(entryIndex);
                    return;
                }

                // ��v���Ȃ���ΑO�̃G���g�����L�^���Ď��̃G���g���ցB
                prevIndex = entryIndex;
                entryIndex = entry.NextInBucket;
            }
        }

        #endregion

        #region �f�[�^�擾

        /// <summary>
        /// �Q�[���I�u�W�F�N�g����S�f�[�^���擾
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
        /// �n�b�V���R�[�h����S�f�[�^���擾
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
        /// �C���f�b�N�X����f�[�^�𒼐ڎ擾�i�ō����j
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
        /// �n�b�V���l����l�̃C���f�b�N�X���擾����B
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

        #region �ʃf�[�^�A�N�Z�X�i�Q�ƕԂ��j

        /// <summary>
        /// �C���f�b�N�X����CharacterBaseInfo���擾�B
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
        /// �C���f�b�N�X����CharacterAtkStatus���擾�B
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
        /// �C���f�b�N�X����CharacterDefStatus���擾�B
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
        /// �C���f�b�N�X����CharacterStateInfo���擾�B
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
        /// �C���f�b�N�X����CharacterStateInfo���擾�B
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
        /// �C���f�b�N�X����MoveStatus���擾�B
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
        /// �C���f�b�N�X����CharaColdLog���擾�B
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

        #region ���[�e�B���e�B

        /// <summary>
        /// ���ׂẴG���g�����N���A
        /// </summary>
        public void Clear()
        {
            // �o�P�b�g�����Z�b�g
            this._buckets.AsSpan().Fill(-1);

            // �G���g���ƃ}�b�s���O���N���A
            this._entries.Clear();
            this._dataIndexToHash.AsSpan().Fill(-1);

            // �f�[�^���N���A�iLength��0�ɂ��邾���j
            this._characterBaseInfo.Length = 0;
            this._characterAtkStatus.Length = 0;
            this._characterDefStatus.Length = 0;
            this._solidData.Length = 0;
            this._characterStateInfo.Length = 0;
            this._moveStatus.Length = 0;
            this._coldLog.Length = 0;

            // �R���g���[���[�z����N���A
            Array.Clear(this._controllers, 0, this._count);

            this._count = 0;
        }

        /// <summary>
        /// �w�肵���L�[�����݂��邩�m�F
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
        /// �w�肵���n�b�V���R�[�h�����݂��邩�m�F
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ContainsKeyByHash(int hashCode)
        {
            return this.TryGetIndexByHash(hashCode, out int _);
        }

        /// <summary>
        /// ���ׂĂ̗L���ȃG���g���ɑ΂��ď��������s
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

        #region �������\�b�h

        /// <summary>
        /// ���������C�A�E�g�̌v�Z�B<br/>
        /// �e�\���̂��Ƃɗv�f�����̃��������C�A�E�g���쐬����B<br/>
        /// �܂��A�e�\���̂̃��C�A�E�g���쐬����ۂ́A64Byte��؂�Ŕz�u�����悤�ɃA���C�����g����B<br/>
        /// 64�o�C�g���ƂɃL���b�V�����C������؂�ꂽ��������ԂŁA�L���b�V�����₷���ʒu�Ƀf�[�^��u�����߂ɁB
        /// </summary>
        private MemoryLayout CalculateMemoryLayout(int capacity)
        {
            MemoryLayout layout = new();
            int currentOffset = 0;

            // CharacterBaseInfo
            layout.BaseInfoOffset = currentOffset;
            currentOffset += capacity * sizeof(CharacterBaseInfo);

            // 64�o�C�g���E�ɃA���C�����g
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
        /// �A���C�����g�v�Z</br>
        /// �e�\���̂̃��C�A�E�g���쐬����ۂ́A64Byte��؂�Ŕz�u�����悤�ɃA���C�����g����K�v������B<br/>
        /// 64�o�C�g���ƂɃL���b�V�����C������؂�ꂽ�������ŁA�L���b�V�����₷���ʒu�Ƀf�[�^��u�����߂ɁB
        /// <param name="memoryOffset">���݂̃������ʒu</param>
        /// <param name="alignment">�������̃A���C�����g�̒P�ʁB2�ׂ̂��悶��Ȃ��Ƃ���</param>
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int AlignTo(int memoryOffset, int alignment)
        {
            // &~ �̔��]�_���ς�2�ׂ̂���� alignment �̒l�ȉ��̉��ʃr�b�g��0�ɂ���}�X�N�A�܂� alignment �̔{���������c��(�������]�肪�S�������邩��)
            // ����ň����̃I�t�Z�b�g�̒l�ɍł��߂� alignment �̔{���𓾂���B
            return (memoryOffset + alignment - 1) & ~(alignment - 1);
        }

        /// <summary>
        /// �o�P�b�g�C���f�b�N�X�̌v�Z
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int GetBucketIndex(int hashCode)
        {
            return (hashCode & 0x7FFFFFFF) % BUCKET_COUNT;
        }

        #endregion

        /// <summary>
        /// �f�R���X�g���N�^�ɂ�肷�ׂẴf�[�^���X�g���^�v���Ƃ��ĕԂ�
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
        /// ���\�[�X�̉��
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