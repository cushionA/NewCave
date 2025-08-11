using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEngine;
using Cysharp.Threading.Tasks;

namespace CharacterController
{
    /// <summary>
    /// エフェクトタイプの統一された列挙型（Long使用で拡張性確保）
    /// ビット演算でカテゴリ分けも可能
    /// 
    /// かさねがけはなし。
    /// その代わり攻撃強化1,2とかつけて別のタイプで共存するように（エフェクトは同じ？）
    /// </summary>
    [Flags]
    public enum EffectType : long
    {
        /// <summary>
        /// エフェクトなし
        /// </summary>
        なし = 0,

        // === デバフ（負の効果）===
        // 状態異常系 (0x8000_0000_0000_0000 ～)

        /// <summary>
        /// 毒：継続的にHPが減少する状態異常
        /// </summary>
        毒 = 1L << 63,

        /// <summary>
        /// 猛毒：毒よりも大きなダメージを与え、スタミナ回復速度も低下させる上位状態異常
        /// </summary>
        猛毒 = 1L << 62,

        /// <summary>
        /// 凍結：キャラクターを停止状態にし、被ダメージを増大させる
        /// </summary>
        凍結 = 1L << 61,

        /// <summary>
        /// 拘束：キャラクターを停止状態にする（凍結より効果時間短め）
        /// </summary>
        拘束 = 1L << 60,

        /// <summary>
        /// 沈黙：魔法の使用を封印する状態異常
        /// </summary>
        沈黙 = 1L << 59,

        /// <summary>
        /// 虚弱：被ダメージ上昇、スタミナ・アーマー回復速度低下、状態異常蓄積解消速度減少
        /// </summary>
        虚弱 = 1L << 58,

        /// <summary>
        /// めまい：与ダメージ減少、ガード性能劣化を引き起こす
        /// </summary>
        めまい = 1L << 57,

        /// <summary>
        /// 呪縛：めまいの上位版で、より強い与ダメージ減少とガード性能劣化
        /// </summary>
        呪縛 = 1L << 56,

        /// <summary>
        /// 刻印：ヘイト上昇と移動速度低下を同時に引き起こす
        /// </summary>
        刻印 = 1L << 55,

        // ステータス低下系 (0x4000_0000_0000_0000 ～)

        /// <summary>
        /// 攻撃力低下：与えるダメージが減少する
        /// </summary>
        攻撃力低下 = 1L << 54,

        /// <summary>
        /// 防御力低下：物理・魔法防御力が減少する
        /// </summary>
        防御力低下 = 1L << 53,

        /// <summary>
        /// 移動速度低下：キャラクターの移動速度が減少する
        /// </summary>
        移動速度低下 = 1L << 52,

        /// <summary>
        /// 被ダメージ増大：受けるダメージが増加する（装備品デメリットにも使用）
        /// </summary>
        被ダメージ増大 = 1L << 51,

        /// <summary>
        /// 与ダメージ減少：与えるダメージが減少する（装備品デメリットにも使用）
        /// </summary>
        与ダメージ減少 = 1L << 50,

        /// <summary>
        /// ヘイト上昇：敵からの注目度（ヘイト値）が上昇する
        /// </summary>
        ヘイト上昇 = 1L << 49,

        /// <summary>
        /// アイテム効果減少：回復アイテムや強化アイテムの効果が減少する
        /// </summary>
        アイテム効果減少 = 1L << 48,

        /// <summary>
        /// 最大HP低下：最大HP値が一時的に減少する
        /// </summary>
        最大HP低下 = 1L << 47,

        // === バフ（正の効果）===
        // 回復・強化系 (0x0000_8000_0000_0000 ～)

        /// <summary>
        /// リジェネ：継続的にHPが回復する
        /// </summary>
        リジェネ = 1L << 31,

        /// <summary>
        /// MPリジェネ：継続的にMPが回復する
        /// </summary>
        MPリジェネ = 1L << 30,

        /// <summary>
        /// 活性：被ダメージ低下、スタミナ・アーマー回復加速、状態異常蓄積減少速度増加
        /// </summary>
        活性 = 1L << 29,

        /// <summary>
        /// 祝福：与ダメージ増大とガード性能強化を同時に得る
        /// </summary>
        祝福 = 1L << 28,

        /// <summary>
        /// 隠密：ヘイト減少、移動速度加速、足音消滅の複合効果
        /// </summary>
        隠密 = 1L << 27,

        // ステータス上昇系 (0x0000_4000_0000_0000 ～)

        /// <summary>
        /// 攻撃力上昇：与えるダメージが増加する
        /// </summary>
        攻撃力上昇 = 1L << 26,

        /// <summary>
        /// 防御力上昇：物理・魔法防御力が上昇する
        /// </summary>
        防御力上昇 = 1L << 25,

        /// <summary>
        /// 移動速度上昇：キャラクターの移動速度が上昇する
        /// </summary>
        移動速度上昇 = 1L << 24,

        /// <summary>
        /// 被ダメージ減少：受けるダメージが減少する
        /// </summary>
        被ダメージ減少 = 1L << 23,

        /// <summary>
        /// 与ダメージ増加：与えるダメージが増加する
        /// </summary>
        与ダメージ増加 = 1L << 22,

        /// <summary>
        /// アクション強化：移動速度加速、二段ジャンプ、特殊回避などのアクション性能向上
        /// </summary>
        アクション強化 = 1L << 21,

        /// <summary>
        /// 特定攻撃強化：魔法、カウンター、各種属性攻撃などの特定攻撃の威力向上
        /// </summary>
        特定攻撃強化 = 1L << 20,

        /// <summary>
        /// アイテム効果増強：回復アイテムや強化アイテムの効果が増加する
        /// </summary>
        アイテム効果増強 = 1L << 19,

        /// <summary>
        /// 最大HP上昇：最大HP値が一時的に上昇する
        /// </summary>
        最大HP上昇 = 1L << 18,

        // 特殊効果系 (0x0000_2000_0000_0000 ～)

        /// <summary>
        /// バリア：一定回数の攻撃を完全に無効化する防御障壁
        /// </summary>
        バリア = 1L << 17,

        /// <summary>
        /// エンチャント：武器に属性効果を付与する（炎、雷、聖、闇など）
        /// </summary>
        エンチャント = 1L << 16,

        /// <summary>
        /// 復活：死亡時に自動的に蘇生する効果（リザオラル相当）
        /// </summary>
        復活 = 1L << 15,

        /// <summary>
        /// 音消滅：足音やアクション音を完全に消去する
        /// </summary>
        音消滅 = 1L << 14,

        /// <summary>
        /// 魔法禁止：魔法やスキルの使用を禁止する
        /// </summary>
        魔法禁止 = 1L << 13,

        // === 即座効果系 ===
        // 回復系 (0x0000_0000_8000_0000 ～)

        /// <summary>
        /// HP回復：即座にHPを回復する
        /// </summary>
        HP回復 = 1L << 7,

        /// <summary>
        /// MP回復：即座にMPを回復する
        /// </summary>
        MP回復 = 1L << 6,

        /// <summary>
        /// スタミナ回復：スタミナを即座に回復する
        /// </summary>
        スタミナ回復 = 1L << 5,

        /// <summary>
        /// アーマー回復：アーマー値を回復する
        /// </summary>
        アーマー回復 = 1L << 4,

        // 解除系 (0x0000_0000_4000_0000 ～)

        /// <summary>
        /// 状態異常解除：毒、凍結などの状態異常を解除する
        /// </summary>
        状態異常解除 = 1L << 3,

        /// <summary>
        /// バフ削除：すべてのバフ効果を削除する
        /// </summary>
        バフ削除 = 1L << 2,

        /// <summary>
        /// 全効果解除：バフ・デバフ問わず全ての効果を解除する
        /// </summary>
        全効果解除 = 1L << 1,

        // === カテゴリマスク ===

        /// <summary>
        /// デバフ判定用のビットマスク
        /// </summary>
        デバフマスク = unchecked((long)0xC000_0000_0000_0000),

        /// <summary>
        /// バフ判定用のビットマスク
        /// </summary>
        バフマスク = 0x3FFF_FFFF_FFFF_FFFF,

        /// <summary>
        /// 状態異常判定用のビットマスク
        /// </summary>
        状態異常マスク = unchecked((long)0x8000_0000_0000_0000),

        /// <summary>
        /// ステータス系効果判定用のビットマスク
        /// </summary>
        ステータス系マスク = 0x7FFF_0000_0000_0000,

        /// <summary>
        /// 即座効果判定用のビットマスク
        /// </summary>
        即座効果マスク = 0x0000_0000_0000_00FF,

        /// <summary>
        /// 重ね掛け可能なエフェクト判定用のビットマスク（数値系エフェクト）
        /// </summary>
        重ね掛け可能マスク = 0x3FFF_0000_0000_0000
    }

    /// <summary>
    /// エフェクトの値の種類を定義する列挙型
    /// </summary>
    public enum EffectValueType : byte
    {
        /// <summary>
        /// フラグ型：ブール値（ON/OFF）で管理される効果（隠密、沈黙など）
        /// </summary>
        フラグ,

        /// <summary>
        /// 加算型：基準値に数値を加算する効果（攻撃力+50など）
        /// </summary>
        加算,

        /// <summary>
        /// 乗算型：基準値に倍率を乗算する効果（攻撃力×1.5など）
        /// </summary>
        乗算,

        /// <summary>
        /// 固定値型：回復量など、固定の数値を使用する効果
        /// </summary>
        固定値
    }

    /// <summary>
    /// エフェクトの終了条件を定義する列挙型
    /// </summary>
    public enum EndConditionType : byte
    {
        /// <summary>
        /// 即座：効果を発揮した直後に終了（回復、解除効果など）
        /// </summary>
        即座,

        /// <summary>
        /// 時間：指定された時間が経過すると終了
        /// </summary>
        時間,

        /// <summary>
        /// 使用回数：指定された回数使用されると終了（攻撃3回まで有効など）
        /// </summary>
        使用回数,

        /// <summary>
        /// 永続：装備解除や死亡まで継続する効果
        /// </summary>
        永続,

        /// <summary>
        /// 条件付き：特定の条件が満たされると終了する効果
        /// </summary>
        条件付き
    }

    /// <summary>
    /// エフェクトの設定データを格納する構造体
    /// </summary>
    [Serializable]
    public struct EffectData
    {
        /// <summary>
        /// エフェクトの種類
        /// </summary>
        public EffectType type;

        /// <summary>
        /// エフェクトの値の種類（フラグ、加算、乗算、固定値）
        /// </summary>
        public EffectValueType valueType;

        /// <summary>
        /// エフェクトの効果量（倍率、加算値、回復量など用途により異なる）
        /// </summary>
        public float value;

        /// <summary>
        /// エフェクトの終了条件
        /// </summary>
        public EndConditionType endCondition;

        /// <summary>
        /// 持続時間（秒）または使用回数（endConditionにより意味が変わる）
        /// </summary>
        public float duration;

        /// <summary>
        /// 表示優先度（高いほど優先的に表示される）
        /// </summary>
        public int priority;

        /// <summary>
        /// エフェクトデータのコンストラクタ
        /// </summary>
        /// <param name="type">エフェクトの種類</param>
        /// <param name="valueType">値の種類</param>
        /// <param name="value">効果量</param>
        /// <param name="endCondition">終了条件</param>
        /// <param name="duration">持続時間または使用回数</param>
        /// <param name="priority">表示優先度</param>
        public EffectData(EffectType type, EffectValueType valueType, float value,
                         EndConditionType endCondition = EndConditionType.時間,
                         float duration = 10f, int priority = 0)
        {
            this.type = type;
            this.valueType = valueType;
            this.value = value;
            this.endCondition = endCondition;
            this.duration = duration;
            this.priority = priority;
        }

        /// <summary>
        /// フラグ型エフェクト用の便利なコンストラクタ
        /// </summary>
        /// <param name="type">エフェクトの種類</param>
        /// <param name="endCondition">終了条件</param>
        /// <param name="duration">持続時間</param>
        /// <param name="priority">表示優先度</param>
        /// <returns>フラグ型のエフェクトデータ</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static EffectData CreateFlag(EffectType type, EndConditionType endCondition = EndConditionType.時間,
                                          float duration = 10f, int priority = 0)
        {
            return new EffectData(type, EffectValueType.フラグ, 1f, endCondition, duration, priority);
        }

        /// <summary>
        /// 数値型エフェクト用の便利なコンストラクタ
        /// </summary>
        /// <param name="type">エフェクトの種類</param>
        /// <param name="valueType">値の種類</param>
        /// <param name="value">効果量</param>
        /// <param name="endCondition">終了条件</param>
        /// <param name="duration">持続時間</param>
        /// <param name="priority">表示優先度</param>
        /// <returns>数値型のエフェクトデータ</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static EffectData CreateValue(EffectType type, EffectValueType valueType, float value,
                                           EndConditionType endCondition = EndConditionType.時間,
                                           float duration = 10f, int priority = 0)
        {
            return new EffectData(type, valueType, value, endCondition, duration, priority);
        }
    }

    /// <summary>
    /// アクティブなエフェクトのインスタンスを管理するクラス
    /// </summary>
    public class ActiveEffect
    {
        /// <summary>
        /// エフェクトの設定データ
        /// </summary>
        public EffectData data;

        /// <summary>
        /// エフェクトが開始された時刻
        /// </summary>
        public float startTime;

        /// <summary>
        /// 残り持続時間（秒）
        /// </summary>
        public float remainingDuration;

        /// <summary>
        /// 残り使用回数
        /// </summary>
        public int remainingUses;

        /// <summary>
        /// このエフェクトがアクティブ状態かどうか
        /// </summary>
        public bool isActive;

        /// <summary>
        /// エフェクトの一意識別子（重ね掛け管理用）
        /// </summary>
        public int effectId;

        /// <summary>
        /// 静的カウンタ（エフェクトID生成用）
        /// インスタンス生成の度に増えていく。
        /// </summary>
        private static int _nextEffectId = 1;

        /// <summary>
        /// アクティブエフェクトのコンストラクタ
        /// </summary>
        /// <param name="data">エフェクトデータ</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ActiveEffect(EffectData data)
        {
            this.data = data;
            this.startTime = Time.time;
            this.remainingDuration = data.duration;
            this.remainingUses = (int)data.duration;
            this.isActive = true;
            this.effectId = _nextEffectId++;
        }

        /// <summary>
        /// このエフェクトが期限切れかどうかを判定する
        /// </summary>
        /// <returns>期限切れの場合true</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsExpired()
        {
            switch ( data.endCondition )
            {
                case EndConditionType.時間:
                    return remainingDuration <= 0;
                case EndConditionType.使用回数:
                    return remainingUses <= 0;
                case EndConditionType.即座:
                    return true;
                case EndConditionType.永続:
                    return false;
                default:
                    return false;
            }
        }
    }

    /// <summary>
    /// キャラクターの参照用インターフェース
    /// EffectSystemがキャラクターに効果の変更を通知するために使用
    /// </summary>
    public interface IMyCharacter
    {
        /// <summary>
        /// エフェクトが追加された際に呼び出されるメソッド
        /// キャラクターはこのメソッド内で追加されたエフェクトに応じた処理を行う
        /// </summary>
        /// <param name="effectType">追加されたエフェクトのタイプ</param>
        void EffectTurnOn(EffectType effectType);

        /// <summary>
        /// エフェクトが削除された際に呼び出されるメソッド
        /// キャラクターはこのメソッド内で削除されたエフェクトに応じた処理を行う
        /// </summary>
        /// <param name="effectType">削除されたエフェクトのタイプ</param>
        void EffectTurnOff(EffectType effectType);
    }

    /// <summary>
    /// シンプルで使いやすいエフェクトシステム
    /// バフ・デバフ・状態異常・特殊効果を統合管理する
    /// ビットフラグによる高速存在チェックと、Listによる効率的なエフェクト管理を行う
    /// </summary>
    public class EffectSystem : MonoBehaviour
    {
        [Header("Settings")]

        /// <summary>
        /// エフェクトの更新間隔（秒）
        /// 小さいほど精密だが処理負荷が増加
        /// </summary>
        [SerializeField] private float _updateInterval = 0.1f;

        /// <summary>
        /// デバッグログを出力するかどうか
        /// </summary>
        [SerializeField] private bool _debugMode = false;

        /// <summary>
        /// キャラクターへの参照（エフェクト変更時の通知用）
        /// </summary>
        private IMyCharacter _myCharacter;

        /// <summary>
        /// 現在アクティブなエフェクトタイプのビットフラグ
        /// 高速な存在チェック用（O(1)での HasEffect 実現）
        /// </summary>
        private long _activeEffectFlags = 0L;

        /// <summary>
        /// 全てのアクティブエフェクトを管理するリスト
        /// 重ね掛け可能エフェクトは同じタイプでも複数保持される
        /// </summary>
        private List<ActiveEffect> _activeEffects = new List<ActiveEffect>();

        // イベント

        /// <summary>
        /// エフェクトが追加された時に発生するイベント
        /// </summary>
        public event Action<EffectType> OnEffectAdded;

        /// <summary>
        /// エフェクトが除去された時に発生するイベント
        /// </summary>
        public event Action<EffectType> OnEffectRemoved;

        /// <summary>
        /// エフェクトの値が変更された時に発生するイベント
        /// </summary>
        public event Action<EffectType, float> OnEffectValueChanged;

        /// <summary>
        /// 初期化処理
        /// </summary>
        private void Start()
        {
            // MyCharacterコンポーネントを取得
            _myCharacter = GetComponent<IMyCharacter>();
            if ( _myCharacter == null )
            {
                Debug.LogError("MyCharacterコンポーネントが見つかりません。IMyCharacterを実装してください。");
            }

            // 定期更新開始
            UpdateEffectsLoop().Forget();
        }

        #region Public Interface

        /// <summary>
        /// エフェクトを追加する
        /// 既存の同タイプエフェクトは新しいもので上書きされる（重ね掛け廃止）
        /// </summary>
        /// <param name="effectData">追加するエフェクトのデータ</param>
        public void AddEffect(EffectData effectData)
        {
            // 即座に効果を発揮するタイプの処理
            if ( effectData.endCondition == EndConditionType.即座 )
            {
                ApplyInstantEffect(effectData);
                return;
            }

            // 既存の同タイプエフェクトを削除（上書きのため）
            bool hadExistingEffect = RemoveEffectInternal(effectData.type);

            // 新しいエフェクトを追加
            var newEffect = new ActiveEffect(effectData);
            _activeEffects.Add(newEffect);

            // ビットフラグを更新
            _activeEffectFlags |= (long)effectData.type;

            // キャラクターに効果追加を通知
            if ( _myCharacter != null )
            {
                _myCharacter.EffectTurnOn(effectData.type);
            }

            OnEffectAdded?.Invoke(effectData.type);

            if ( _debugMode )
                Debug.Log($"エフェクト追加: {effectData.type} (値: {effectData.value}, 持続時間: {effectData.duration}, ID: {newEffect.effectId}, 上書き: {hadExistingEffect})");
        }

        /// <summary>
        /// 特定のエフェクトタイプを全て除去する
        /// </summary>
        /// <param name="type">除去するエフェクトのタイプ</param>
        public void RemoveEffect(EffectType type)
        {
            var removedEffects = new List<EffectType>();

            // 逆順でループして安全に削除
            for ( int i = _activeEffects.Count - 1; i >= 0; i-- )
            {
                if ( _activeEffects[i].data.type == type )
                {
                    removedEffects.Add(_activeEffects[i].data.type);
                    _activeEffects.RemoveAt(i);
                }
            }

            if ( removedEffects.Count > 0 )
            {
                // ビットフラグから該当タイプを削除
                _activeEffectFlags &= ~(long)type;

                // キャラクターに効果終了を通知（削除されたエフェクトを1つずつ）
                if ( _myCharacter != null )
                {
                    foreach ( var removedType in removedEffects )
                    {
                        _myCharacter.EffectTurnOff(removedType);
                    }
                }

                OnEffectRemoved?.Invoke(type);

                if ( _debugMode )
                    Debug.Log($"エフェクト除去: {type} ({removedEffects.Count}個)");
            }
        }

        /// <summary>
        /// 内部的にエフェクトを削除する（通知なし）
        /// AddEffectでの上書き処理で使用
        /// </summary>
        /// <param name="type">削除するエフェクトのタイプ</param>
        /// <returns>削除されたエフェクトがあった場合true</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool RemoveEffectInternal(EffectType type)
        {
            bool removed = false;

            // 逆順でループして安全に削除
            for ( int i = _activeEffects.Count - 1; i >= 0; i-- )
            {
                if ( _activeEffects[i].data.type == type )
                {
                    _activeEffects.RemoveAt(i);
                    removed = true;
                }
            }

            // ビットフラグから該当タイプを削除
            if ( removed )
            {
                _activeEffectFlags &= ~(long)type;
            }

            return removed;
        }

        /// <summary>
        /// 特定のエフェクトIDのエフェクトを除去する（重ね掛け可能エフェクト用）
        /// </summary>
        /// <param name="effectId">除去するエフェクトのID</param>
        public void RemoveEffectById(int effectId)
        {
            for ( int i = _activeEffects.Count - 1; i >= 0; i-- )
            {
                if ( _activeEffects[i].effectId == effectId )
                {
                    var removedType = _activeEffects[i].data.type;
                    _activeEffects.RemoveAt(i);

                    // ビットフラグから該当タイプを削除
                    _activeEffectFlags &= ~(long)removedType;

                    // キャラクターに効果終了を通知
                    if ( _myCharacter != null )
                    {
                        _myCharacter.EffectTurnOff(removedType);
                    }

                    OnEffectRemoved?.Invoke(removedType);

                    if ( _debugMode )
                        Debug.Log($"エフェクト除去: {removedType} (ID: {effectId})");

                    break;
                }
            }
        }

        /// <summary>
        /// 指定されたカテゴリに属するエフェクトを全て除去する
        /// </summary>
        /// <param name="categoryMask">カテゴリを指定するビットマスク</param>
        public void RemoveEffectsByCategory(EffectType categoryMask)
        {
            var removedEffects = new List<EffectType>();
            var removedTypes = new HashSet<EffectType>();
            long removedBits = 0L;

            // 逆順でループして安全に削除
            for ( int i = _activeEffects.Count - 1; i >= 0; i-- )
            {
                if ( (_activeEffects[i].data.type & categoryMask) != 0 )
                {
                    var effectType = _activeEffects[i].data.type;
                    removedEffects.Add(effectType);
                    removedTypes.Add(effectType);
                    removedBits |= (long)effectType;
                    _activeEffects.RemoveAt(i);
                }
            }

            if ( removedEffects.Count > 0 )
            {
                // 削除されたタイプのビットをまとめて削除
                _activeEffectFlags &= ~removedBits;

                // キャラクターに効果終了を通知（削除されたエフェクトを1つずつ）
                if ( _myCharacter != null )
                {
                    foreach ( var removedEffect in removedEffects )
                    {
                        _myCharacter.EffectTurnOff(removedEffect);
                    }
                }

                // 除去されたタイプごとにイベント発火
                foreach ( var type in removedTypes )
                {
                    OnEffectRemoved?.Invoke(type);
                }

                if ( _debugMode )
                    Debug.Log($"カテゴリエフェクト除去: 0x{(long)categoryMask:X} ({removedEffects.Count}個)");
            }
        }

        /// <summary>
        /// 全てのデバフを除去する
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RemoveAllDebuffs()
        {
            RemoveEffectsByCategory(EffectType.デバフマスク);
        }

        /// <summary>
        /// 全てのバフを除去する
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RemoveAllBuffs()
        {
            RemoveEffectsByCategory(EffectType.バフマスク);
        }

        /// <summary>
        /// 指定されたエフェクトが存在するかをチェックする（O(1) の高速チェック）
        /// </summary>
        /// <param name="type">チェックするエフェクトのタイプ</param>
        /// <returns>エフェクトが存在する場合true</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool HasEffect(EffectType type)
        {
            return (_activeEffectFlags & (long)type) != 0;
        }

        /// <summary>
        /// フラグ型エフェクトの状態を取得する
        /// </summary>
        /// <param name="type">取得するエフェクトのタイプ</param>
        /// <returns>フラグ型エフェクトがアクティブな場合true</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool GetEffectFlag(EffectType type)
        {
            // まずビットフラグで高速チェック
            if ( (_activeEffectFlags & (long)type) == 0 )
                return false;

            // 詳細チェック：フラグ型かつアクティブなエフェクトがあるか
            for ( int i = 0; i < _activeEffects.Count; i++ )
            {
                var effect = _activeEffects[i];
                if ( effect.data.type == type && effect.data.valueType == EffectValueType.フラグ && effect.isActive )
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// エフェクトの値を取得する（重ね掛け廃止により同タイプは1つのみ）
        /// </summary>
        /// <param name="type">取得するエフェクトのタイプ</param>
        /// <param name="defaultValue">エフェクトが存在しない場合のデフォルト値</param>
        /// <returns>エフェクトの値（存在しない場合はデフォルト値）</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float GetEffectValue(EffectType type, float defaultValue = 0f)
        {
            // まずビットフラグで高速チェック
            if ( (_activeEffectFlags & (long)type) == 0 )
                return defaultValue;

            // 該当するエフェクトを検索（重ね掛け廃止により1つのみ）
            for ( int i = 0; i < _activeEffects.Count; i++ )
            {
                var effect = _activeEffects[i];
                if ( effect.data.type == type && effect.isActive )
                {
                    if ( effect.data.valueType == EffectValueType.フラグ )
                    {
                        return 1f; // フラグ型はアクティブなら1
                    }

                    return effect.data.value;
                }
            }

            return defaultValue;
        }

        /// <summary>
        /// ステータス計算用：指定タイプの効果を基準値に適用して計算する（重ね掛け廃止により1つのみ）
        /// </summary>
        /// <param name="effectType">計算に使用するエフェクトのタイプ</param>
        /// <param name="baseValue">基準値</param>
        /// <returns>エフェクトを適用した結果の値</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float CalculateStatModifier(EffectType effectType, float baseValue)
        {
            // まずビットフラグで高速チェック
            if ( (_activeEffectFlags & (long)effectType) == 0 )
                return baseValue;

            // 該当するエフェクトを検索（重ね掛け廃止により1つのみ）
            for ( int i = 0; i < _activeEffects.Count; i++ )
            {
                var effect = _activeEffects[i];
                if ( effect.data.type == effectType && effect.isActive )
                {
                    switch ( effect.data.valueType )
                    {
                        case EffectValueType.加算:
                            return baseValue + effect.data.value;
                        case EffectValueType.乗算:
                            return baseValue * effect.data.value;
                        case EffectValueType.固定値:
                            return effect.data.value;
                        case EffectValueType.フラグ:
                            return effect.isActive ? baseValue : 0f;
                    }
                }
            }

            return baseValue;
        }

        /// <summary>
        /// 複数のエフェクトタイプでステータスを計算する
        /// 加算効果を先に適用し、その後乗算効果を適用する（重ね掛け廃止により各タイプ1つのみ）
        /// </summary>
        /// <param name="baseValue">基準値。ステータスから抜く</param>
        /// <param name="effectTypes">適用するエフェクトタイプの配列</param>
        /// <returns>全てのエフェクトを適用した結果の値</returns>
        public float CalculateStatWithMultipleEffects(float baseValue, params EffectType[] effectTypes)
        {
            float result = baseValue;

            // 加算効果を先に適用
            for ( int j = 0; j < effectTypes.Length; j++ )
            {
                var effectType = effectTypes[j];
                if ( (_activeEffectFlags & (long)effectType) != 0 )
                {
                    for ( int i = 0; i < _activeEffects.Count; i++ )
                    {
                        var effect = _activeEffects[i];
                        if ( effect.data.type == effectType && effect.isActive && effect.data.valueType == EffectValueType.加算 )
                        {
                            result += effect.data.value;
                            break; // 重ね掛け廃止により1つのみ
                        }
                    }
                }
            }

            // 乗算効果を後に適用
            for ( int j = 0; j < effectTypes.Length; j++ )
            {
                var effectType = effectTypes[j];
                if ( (_activeEffectFlags & (long)effectType) != 0 )
                {
                    for ( int i = 0; i < _activeEffects.Count; i++ )
                    {
                        var effect = _activeEffects[i];
                        if ( effect.data.type == effectType && effect.isActive && effect.data.valueType == EffectValueType.乗算 )
                        {
                            result *= effect.data.value;
                            break; // 重ね掛け廃止により1つのみ
                        }
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// アクティブなエフェクトの一覧を取得する（UI表示用）
        /// </summary>
        /// <param name="sortByPriority">優先度順にソートするかどうか</param>
        /// <returns>アクティブなエフェクトのリスト</returns>
        public List<ActiveEffect> GetActiveEffects(bool sortByPriority = true)
        {
            var effects = new List<ActiveEffect>();

            for ( int i = 0; i < _activeEffects.Count; i++ )
            {
                if ( _activeEffects[i].isActive )
                {
                    effects.Add(_activeEffects[i]);
                }
            }

            if ( sortByPriority )
            {
                effects.Sort((a, b) => b.data.priority.CompareTo(a.data.priority));
            }

            return effects;
        }

        /// <summary>
        /// エフェクトの使用回数を消費する（重ね掛け廃止により1つのみ）
        /// 使用回数制限のあるエフェクトで使用される
        /// </summary>
        /// <param name="type">使用回数を消費するエフェクトのタイプ</param>
        /// <param name="amount">消費する回数</param>
        public void ConsumeEffectUse(EffectType type, int amount = 1)
        {
            // まずビットフラグで高速チェック
            if ( (_activeEffectFlags & (long)type) == 0 )
                return;

            // 該当するエフェクトを検索（重ね掛け廃止により1つのみ）
            for ( int i = 0; i < _activeEffects.Count; i++ )
            {
                var effect = _activeEffects[i];
                if ( effect.data.type == type && effect.data.endCondition == EndConditionType.使用回数 )
                {
                    effect.remainingUses -= amount;
                    if ( effect.remainingUses <= 0 )
                    {
                        RemoveEffectById(effect.effectId);
                    }
                    break; // 重ね掛け廃止により1つのみ
                }
            }
        }

        /// <summary>
        /// エフェクトが存在し、使用回数制限がある場合に使用回数を消費する
        /// 存在チェックと使用回数消費を同時に行う効率的なメソッド（重ね掛け廃止により1つのみ）
        /// </summary>
        /// <param name="type">消費するエフェクトのタイプ</param>
        /// <param name="amount">消費する回数</param>
        /// <returns>エフェクトが存在し、使用回数を消費した場合true</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryConsumeEffect(EffectType type, int amount = 1)
        {
            // まずビットフラグで高速チェック
            if ( (_activeEffectFlags & (long)type) == 0 )
                return false;

            // 該当するエフェクトを検索（重ね掛け廃止により1つのみ）
            for ( int i = 0; i < _activeEffects.Count; i++ )
            {
                var effect = _activeEffects[i];
                if ( effect.data.type == type && effect.data.endCondition == EndConditionType.使用回数 )
                {
                    effect.remainingUses -= amount;

                    if ( effect.remainingUses <= 0 )
                    {
                        RemoveEffectById(effect.effectId);
                    }

                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// エフェクトが存在し、使用回数制限がある場合のみtrueを返す（消費はしない）
        /// 使用前の事前チェック用（重ね掛け廃止により1つのみ）
        /// </summary>
        /// <param name="type">チェックするエフェクトのタイプ</param>
        /// <returns>エフェクトが存在し、使用回数制限がある場合true</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool HasConsumableEffect(EffectType type)
        {
            // まずビットフラグで高速チェック
            if ( (_activeEffectFlags & (long)type) == 0 )
                return false;

            // 該当するエフェクトを検索（重ね掛け廃止により1つのみ）
            for ( int i = 0; i < _activeEffects.Count; i++ )
            {
                var effect = _activeEffects[i];
                if ( effect.data.type == type && effect.data.endCondition == EndConditionType.使用回数 )
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 使用回数制限エフェクトの残り使用回数を取得する（重ね掛け廃止により1つのみ）
        /// </summary>
        /// <param name="type">チェックするエフェクトのタイプ</param>
        /// <returns>残り使用回数（エフェクトが存在しない場合は0）</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetRemainingUses(EffectType type)
        {
            // まずビットフラグで高速チェック
            if ( (_activeEffectFlags & (long)type) == 0 )
                return 0;

            // 該当するエフェクトを検索
            for ( int i = 0; i < _activeEffects.Count; i++ )
            {
                var effect = _activeEffects[i];
                if ( effect.data.type == type && effect.data.endCondition == EndConditionType.使用回数 )
                {
                    return effect.remainingUses;
                }
            }

            return 0;
        }

        #endregion

        #region Update & Management

        /// <summary>
        /// エフェクトの定期更新を行う非同期ループ
        /// 時間経過によるエフェクトの管理を行う
        /// </summary>
        /// <returns>UniTask</returns>
        private async UniTaskVoid UpdateEffectsLoop()
        {
            while ( this != null && gameObject.activeInHierarchy )
            {
                UpdateEffects();
                await UniTask.Delay(TimeSpan.FromSeconds(_updateInterval));
            }
        }

        /// <summary>
        /// エフェクトの更新処理
        /// 持続時間の減少と期限切れエフェクトの除去を行う
        /// </summary>
        private void UpdateEffects()
        {
            // stackallocで小さなバッファを高速確保（ヒープ割り当てなし）
            Span<int> expiredEffectIds = stackalloc int[32]; // 通常は十分な容量
            int expiredCount = 0;

            for ( int i = 0; i < _activeEffects.Count; i++ )
            {
                var effect = _activeEffects[i];

                // 時間経過処理
                if ( effect.data.endCondition == EndConditionType.時間 )
                {
                    effect.remainingDuration -= _updateInterval;
                }

                // 期限切れチェック
                if ( effect.IsExpired() )
                {
                    if ( expiredCount < expiredEffectIds.Length )
                    {
                        expiredEffectIds[expiredCount++] = effect.effectId;
                    }
                    else
                    {
                        // バッファが満杯になったらループを終了
                        // 残りのエフェクトは次フレームで処理
                        if ( _debugMode )
                            Debug.Log($"エフェクトバッファ満杯。{expiredCount}個を削除後、残りは次フレームで処理");
                        break;
                    }
                }
            }

            // 期限切れエフェクトを除去（Spanを直接使用）
            for ( int i = 0; i < expiredCount; i++ )
            {
                RemoveEffectById(expiredEffectIds[i]);
            }
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// 即座に効果を発揮するエフェクトの処理
        /// HP回復、解除効果などの即時効果を実行する
        /// </summary>
        /// <param name="effectData">実行するエフェクトのデータ</param>
        private void ApplyInstantEffect(EffectData effectData)
        {
            switch ( effectData.type )
            {
                case EffectType.HP回復:
                    // HP回復処理
                    if ( _debugMode )
                        Debug.Log($"HP回復: {effectData.value}");
                    break;

                case EffectType.MP回復:
                    // MP回復処理
                    if ( _debugMode )
                        Debug.Log($"MP回復: {effectData.value}");
                    break;

                case EffectType.状態異常解除:
                    RemoveEffectsByCategory(EffectType.状態異常マスク);
                    break;

                case EffectType.バフ削除:
                    RemoveAllBuffs();
                    break;

                case EffectType.全効果解除:
                    // 全効果解除の場合、削除される全エフェクトを記録して個別通知
                    var allEffectsToRemove = new List<EffectType>();
                    for ( int i = 0; i < _activeEffects.Count; i++ )
                    {
                        allEffectsToRemove.Add(_activeEffects[i].data.type);
                    }

                    _activeEffects.Clear();
                    _activeEffectFlags = 0L;

                    // 削除されたエフェクトを1つずつ通知
                    if ( _myCharacter != null )
                    {
                        foreach ( var removedType in allEffectsToRemove )
                        {
                            _myCharacter.EffectTurnOff(removedType);
                        }
                    }
                    return; // 早期リターンで下のmyCharacter呼び出しをスキップ
            }

            if ( _debugMode )
                Debug.Log($"即座効果適用: {effectData.type} (値: {effectData.value})");
        }

        #endregion

        #region Debug & Utility

        /// <summary>
        /// デバッグ用：現在アクティブなエフェクトを表示する
        /// </summary>
        [ContextMenu("デバッグ - アクティブエフェクト表示")]
        private void DebugShowActiveEffects()
        {
            Debug.Log("=== アクティブエフェクト ===");
            Debug.Log($"アクティブフラグ: 0x{_activeEffectFlags:X16}");
            Debug.Log($"エフェクト数: {_activeEffects.Count}");

            var effectGroups = new Dictionary<EffectType, List<ActiveEffect>>();

            // タイプ別にグループ化
            for ( int i = 0; i < _activeEffects.Count; i++ )
            {
                var effect = _activeEffects[i];
                if ( !effectGroups.ContainsKey(effect.data.type) )
                {
                    effectGroups[effect.data.type] = new List<ActiveEffect>();
                }
                effectGroups[effect.data.type].Add(effect);
            }

            // グループ別に表示
            foreach ( var group in effectGroups )
            {
                if ( group.Value.Count == 1 )
                {
                    var effect = group.Value[0];
                    Debug.Log($"{effect.data.type}: 値={effect.data.value}, 残り時間={effect.remainingDuration:F1}秒, ID={effect.effectId}");
                }
                else
                {
                    Debug.Log($"{group.Key}: {group.Value.Count}個のエフェクト（重ね掛け）");
                    foreach ( var effect in group.Value )
                    {
                        Debug.Log($"  値={effect.data.value}, 残り時間={effect.remainingDuration:F1}秒, ID={effect.effectId}");
                    }
                }
            }
        }

        /// <summary>
        /// デバッグ用：テスト用のバフを追加する
        /// </summary>
        [ContextMenu("デバッグ - テストバフ追加")]
        private void DebugAddTestBuff()
        {
            AddEffect(EffectPresets.攻撃力強化(1.5f, 30f));
        }

        /// <summary>
        /// デバッグ用：テスト用のデバフを追加する
        /// </summary>
        [ContextMenu("デバッグ - テストデバフ追加")]
        private void DebugAddTestDebuff()
        {
            AddEffect(EffectPresets.毒効果(5f, 15f));
        }

        /// <summary>
        /// デバッグ用：複数の攻撃力バフを順次追加する（上書きテスト）
        /// </summary>
        [ContextMenu("デバッグ - 攻撃力バフ上書きテスト")]
        private void DebugAddMultipleAttackBuffs()
        {
            // 重ね掛け廃止により、各エフェクトは前のものを上書きする
            AddEffect(EffectPresets.攻撃力強化(1.2f, 60f));
            Debug.Log("攻撃力1.2倍（60秒）を追加");

            AddEffect(EffectPresets.攻撃力強化(1.5f, 30f));
            Debug.Log("攻撃力1.5倍（30秒）で上書き");

            AddEffect(EffectPresets.攻撃力強化(2.0f, 15f));
            Debug.Log("攻撃力2.0倍（15秒）で上書き");
        }

        /// <summary>
        /// デバッグ用：使用回数制限エフェクトを追加する
        /// </summary>
        [ContextMenu("デバッグ - 使用回数制限エフェクト追加")]
        private void DebugAddConsumableEffect()
        {
            var consumableEffect = EffectData.CreateValue(
                EffectType.特定攻撃強化,
                EffectValueType.乗算,
                2.0f,
                EndConditionType.使用回数,
                5f, // 5回使用可能
                6
            );
            AddEffect(consumableEffect);
        }

        /// <summary>
        /// デバッグ用：TryConsumeEffectのテスト
        /// </summary>
        [ContextMenu("デバッグ - TryConsumeEffectテスト")]
        private void DebugTryConsumeEffect()
        {
            bool consumed = TryConsumeEffect(EffectType.特定攻撃強化);
            Debug.Log($"エフェクト消費: {consumed}");

            int remaining = GetRemainingUses(EffectType.特定攻撃強化);
            Debug.Log($"残り使用回数: {remaining}");
        }

        /// <summary>
        /// デバッグ用：EffectTurnOnとEffectTurnOffのテスト
        /// </summary>
        [ContextMenu("デバッグ - エフェクト通知テスト")]
        private void DebugEffectNotificationTest()
        {
            // テスト用エフェクトを追加
            AddEffect(EffectPresets.攻撃力強化(1.5f, 10f));
            AddEffect(EffectPresets.毒効果(5f, 15f));

            // 少し待ってからデバフを削除
            StartCoroutine(DebugRemoveDebuffsDelayed());
        }

        private System.Collections.IEnumerator DebugRemoveDebuffsDelayed()
        {
            yield return new WaitForSeconds(2f);
            Debug.Log("=== デバフ一括削除テスト ===");
            RemoveAllDebuffs();
        }

        /// <summary>
        /// デバッグ用：HasEffectの性能テスト
        /// </summary>
        [ContextMenu("デバッグ - HasEffect性能テスト")]
        private void DebugHasEffectPerformanceTest()
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            // 100万回のHasEffectチェック
            for ( int i = 0; i < 1000000; i++ )
            {
                bool hasAttackBuff = HasEffect(EffectType.攻撃力上昇);
                bool hasPoison = HasEffect(EffectType.毒);
                bool hasSilence = HasEffect(EffectType.沈黙);
            }

            stopwatch.Stop();
            Debug.Log($"HasEffect 100万回実行時間: {stopwatch.ElapsedMilliseconds}ms");
        }

        #endregion
    }

    #region Helper Classes

    /// <summary>
    /// よく使用されるエフェクトデータのプリセット集
    /// 定型的なエフェクトを簡単に作成するためのヘルパークラス
    /// </summary>
    public static class EffectPresets
    {
        // === バフ系プリセット ===

        /// <summary>
        /// 攻撃力強化エフェクトを作成する
        /// </summary>
        /// <param name="multiplier">攻撃力の倍率</param>
        /// <param name="duration">持続時間（秒）</param>
        /// <returns>攻撃力強化エフェクト</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static EffectData 攻撃力強化(float multiplier, float duration) =>
            EffectData.CreateValue(EffectType.攻撃力上昇, EffectValueType.乗算, multiplier, EndConditionType.時間, duration, 3);

        /// <summary>
        /// 防御力強化エフェクトを作成する
        /// </summary>
        /// <param name="multiplier">防御力の倍率</param>
        /// <param name="duration">持続時間（秒）</param>
        /// <returns>防御力強化エフェクト</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static EffectData 防御力強化(float multiplier, float duration) =>
            EffectData.CreateValue(EffectType.防御力上昇, EffectValueType.乗算, multiplier, EndConditionType.時間, duration, 3);

        /// <summary>
        /// 移動速度強化エフェクトを作成する
        /// </summary>
        /// <param name="multiplier">移動速度の倍率</param>
        /// <param name="duration">持続時間（秒）</param>
        /// <returns>移動速度強化エフェクト</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static EffectData 移動速度強化(float multiplier, float duration) =>
            EffectData.CreateValue(EffectType.移動速度上昇, EffectValueType.乗算, multiplier, EndConditionType.時間, duration, 2);

        /// <summary>
        /// 隠密効果を作成する（フラグ型）
        /// </summary>
        /// <param name="duration">持続時間（秒）</param>
        /// <returns>隠密エフェクト</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static EffectData 隠密効果(float duration) =>
            EffectData.CreateFlag(EffectType.隠密, EndConditionType.時間, duration, 4);

        // === デバフ系プリセット ===

        /// <summary>
        /// 毒効果を作成する
        /// </summary>
        /// <param name="damagePerSecond">1秒あたりのダメージ量</param>
        /// <param name="duration">持続時間（秒）</param>
        /// <returns>毒エフェクト</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static EffectData 毒効果(float damagePerSecond, float duration) =>
            EffectData.CreateValue(EffectType.毒, EffectValueType.固定値, damagePerSecond, EndConditionType.時間, duration, 8);

        /// <summary>
        /// 虚弱効果を作成する
        /// </summary>
        /// <param name="multiplier">各種ステータスの倍率</param>
        /// <param name="duration">持続時間（秒）</param>
        /// <returns>虚弱エフェクト</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static EffectData 虚弱効果(float multiplier, float duration) =>
            EffectData.CreateValue(EffectType.虚弱, EffectValueType.乗算, multiplier, EndConditionType.時間, duration, 6);

        /// <summary>
        /// 沈黙効果を作成する（フラグ型）
        /// </summary>
        /// <param name="duration">持続時間（秒）</param>
        /// <returns>沈黙エフェクト</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static EffectData 沈黙効果(float duration) =>
            EffectData.CreateFlag(EffectType.沈黙, EndConditionType.時間, duration, 7);

        // === 即座効果系プリセット ===

        /// <summary>
        /// HP回復効果を作成する
        /// </summary>
        /// <param name="amount">回復量</param>
        /// <returns>HP回復エフェクト</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static EffectData HP回復(float amount) =>
            EffectData.CreateValue(EffectType.HP回復, EffectValueType.固定値, amount, EndConditionType.即座, 0f, 0);

        /// <summary>
        /// MP回復効果を作成する
        /// </summary>
        /// <param name="amount">回復量</param>
        /// <returns>MP回復エフェクト</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static EffectData MP回復(float amount) =>
            EffectData.CreateValue(EffectType.MP回復, EffectValueType.固定値, amount, EndConditionType.即座, 0f, 0);

        /// <summary>
        /// 状態異常全解除効果を作成する
        /// </summary>
        /// <returns>状態異常解除エフェクト</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static EffectData 状態異常全解除() =>
            EffectData.CreateFlag(EffectType.状態異常解除, EndConditionType.即座, 0f, 0);
    }

    #endregion
}