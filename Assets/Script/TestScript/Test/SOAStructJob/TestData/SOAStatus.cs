using MyTool.Collections;
using Sirenix.OdinInspector;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections;
using UnityEngine;
using static CharacterController.AIManager;
using Unity.Mathematics;
using static CharacterController.BrainStatus;
using CharacterController;


namespace TestScript.SOATest
{
    /// <summary>
    /// 使用場面に基づいてデータを構造体に切り分けている。<br/>
    /// 
    /// このクラスではキャラクターの種類ごとの設定データを定義している。<br/>
    /// いわゆるステータスデータ。<br/>
    /// 可変部分（座標やHP）、キャラクターの意思決定に使用する部分（判断条件等）はJobシステムで使用する前提で値型のみで構成。<br/>
    /// 他の部分はScriptableObjectだけが不変の共有データとして持っていればいいため、参照型(エフェクトのデータなど)も使う。<br/>
    /// 
    /// 管理方針
    /// 更新されるデータ：SOA構造のキャラデータ保管庫で管理
    /// 固定データ（値型）：キャラの種類ごとにキャラデータを収めた配列をScriptableで作成し、MemCpyでNativeArrayに引っ張る。
    /// 　　　　　　        シスターさんみたいな作戦が変更されるやつは、最大値で事前にバッファしておく。
    /// 固定データ（参照型）：キャラの種類ごとにキャラデータを収めた配列をScriptableで持っておく。
    /// 
    /// 共通：キャラの種類ごとにキャラデータを収めた配列にはキャラIDでアクセスする。
    /// </summary>
    [CreateAssetMenu(fileName = "SOAStatus", menuName = "Scriptable Objects/SOAStatus")]
    public class SOAStatus : SerializedScriptableObject
    {
        #region Enum定義

        /// <summary>
        /// 自分が行動を決定するための条件
        /// ○○の時、攻撃・回復・支援・逃走・護衛など
        /// 対象が自分、味方、敵のどれかという区分と否定（以上が以内になったり）フラグの組み合わせで表現 - > IsInvertフラグ
        /// 味方が死んだとき、は死亡状態で数秒キャラを残すことで、死亡を条件にしたフィルターにかかるようにするか。
        /// </summary>
        public enum ActJudgeCondition
        {
            指定のヘイト値の敵がいる時 = 1,
            //対象が一定数の時 = 2, // フィルターも活用することで、ここでかなりの数の単純な条件はやれる。一体以上条件でタイプフィルターで対象のタイプ絞ったり
            HPが一定割合の対象がいる時 = 2,
            MPが一定割合の対象がいる時 = 3,
            設定距離に対象がいる時 = 4,  //距離系の処理は別のやり方で事前にキャッシュを行う。AIの設定の範囲だけセンサーで調べる方法をとる。判断時にやるようにする？
            特定の属性で攻撃する対象がいる時 = 5,
            特定の数の敵に狙われている時 = 6,// 陣営フィルタリングは有効
            条件なし = 0 // 何も当てはまらなかった時の補欠条件。
        }

        /// <summary>
        /// 行動判断をする前に
        /// 自分のMPやHPの割合などの、自分に関する前提条件を判断するための設定
        /// </summary>
        public enum SkipJudgeCondition
        {
            自分のHPが一定割合の時,
            自分のMPが一定割合の時,
            条件なし // 何も当てはまらなかった時の補欠条件。
        }

        /// <summary>
        /// MoveJudgeConditionの対象のタイプ
        /// </summary>
        public enum TargetType
        {
            自分 = 0,
            味方 = 1,
            敵 = 2
        }

        /// <summary>
        /// 判断の結果選択される行動のタイプ。
        /// 
        /// </summary>
        [Flags]
        public enum ActState
        {
            指定なし = 0,// ステートフィルター判断で使う。何も指定しない。
            追跡 = 1 << 0,
            逃走 = 1 << 1,
            攻撃 = 1 << 2,
            待機 = 1 << 3,// 攻撃後のクールタイム中など。この状態で動作する回避率を設定する？
            防御 = 1 << 4,// 動き出す距離を設定できるようにする？ その場で基本ガードだけど、相手がいくらか離れたら動き出す、的な
            支援 = 1 << 5,
            回復 = 1 << 6,
            集合 = 1 << 7,// 特定の味方の場所に行く。集合後に防御に移行するロジックを組めば護衛にならない？
        }

        /// <summary>
        /// 敵に対するヘイト値の上昇、減少の条件。
        /// 条件に当てはまる敵のヘイト値が上昇したり減少したりする。
        /// あるいは味方の支援・回復・護衛対象を決める
        /// これも否定フラグとの組み合わせで使う
        /// </summary>
        public enum TargetSelectCondition
        {
            高度,
            HP割合,
            HP,
            敵に狙われてる数,//一番狙われてるか、狙われてないか
            合計攻撃力,
            合計防御力,
            斬撃攻撃力,//特定の属性の攻撃力が一番高い/低いヤツ
            刺突攻撃力,
            打撃攻撃力,
            炎攻撃力,
            雷攻撃力,
            光攻撃力,
            闇攻撃力,
            斬撃防御力,
            刺突防御力,
            打撃防御力,
            炎防御力,
            雷防御力,
            光防御力,
            闇防御力,
            距離,
            自分,
            プレイヤー,
            指定なし_ヘイト値, // 基本の条件。対象の中で最もヘイト高い相手を攻撃する。
            不要_状態変更// モードチェンジする。
        }

        /// <summary>
        /// キャラクターの属性。
        /// ここに当てはまる分全部ぶち込む。
        /// </summary>
        [Flags]
        public enum CharacterFeature
        {
            プレイヤー = 1 << 0,
            シスターさん = 1 << 1,
            NPC = 1 << 2,
            通常エネミー = 1 << 3,
            ボス = 1 << 4,
            兵士 = 1 << 5,//陸の雑兵
            飛行 = 1 << 6,//飛ぶやつ
            射手 = 1 << 7,//遠距離
            騎士 = 1 << 8,//盾持ち
            罠系 = 1 << 9,//待ち構えてるやつ
            強敵 = 1 << 10,// 強敵
            ザコ = 1 << 11,
            ヒーラー = 1 << 12,
            サポーター = 1 << 13,
            高速 = 1 << 14,
            指揮官 = 1 << 15,
            指定なし = 0//指定なし
        }

        /// <summary>
        /// キャラクターが所属する陣営
        /// </summary>
        public enum CharacterSide
        {
            プレイヤー = 0,// 味方
            魔物 = 1,// 一般的な敵
            その他 = 2,// それ以外
            指定なし = 3
        }

        /// <summary>
        /// 属性の列挙型
        /// 状態異常は分ける。
        /// </summary>
        [Flags]
        public enum Element
        {
            斬撃属性 = 1 << 0,
            刺突属性 = 1 << 1,
            打撃属性 = 1 << 2,
            聖属性 = 1 << 3,
            闇属性 = 1 << 4,
            炎属性 = 1 << 5,
            雷属性 = 1 << 6,
            指定なし = 0
        }

        /// <summary>
        /// キャラのレベル
        /// このレベルが高いと他の敵に邪魔されない
        /// </summary>
        public enum CharacterRank
        {
            ザコ,//雑魚
            主力級,//基本はこれ
            指揮官,//強モブ
            ボス//ボスだけ
        }

        /// <summary>
        /// 特殊状態
        /// </summary>
        [Flags]
        public enum SpecialEffect
        {
            ヘイト増大 = 1 << 1,
            ヘイト減少 = 1 << 2,
            なし = 0,
        }

        /// <summary>
        /// bitableな真偽値
        /// Jobシステム、というよりネイティブコードと bool の相性が良くないため実装
        /// </summary>
        public enum BitableBool
        {
            FALSE = 0,
            TRUE = 1
        }

        #endregion Enum定義

        #region 構造体定義

        /// <summary>
        /// 各属性に対する攻撃力または防御力の値を保持する構造体
        /// SoA OK
        /// </summary>
        [Serializable]
        [StructLayout(LayoutKind.Sequential)]
        public struct ElementalStatus
        {
            /// <summary>
            /// 斬撃属性の値
            /// </summary>
            [Header("斬撃属性")]
            public int slash;

            /// <summary>
            /// 刺突属性の値
            /// </summary>
            [Header("刺突属性")]
            public int pierce;

            /// <summary>
            /// 打撃属性の値
            /// </summary>
            [Header("打撃属性")]
            public int strike;

            /// <summary>
            /// 炎属性の値
            /// </summary>
            [Header("炎属性")]
            public int fire;

            /// <summary>
            /// 雷属性の値
            /// </summary>
            [Header("雷属性")]
            public int lightning;

            /// <summary>
            /// 光属性の値
            /// </summary>
            [Header("光属性")]
            public int light;

            /// <summary>
            /// 闇属性の値
            /// </summary>
            [Header("闇属性")]
            public int dark;

            /// <summary>
            /// 合計値を返す。
            /// </summary>
            /// <returns></returns>
            public int ReturnSum()
            {
                return this.slash + this.pierce + this.strike + this.fire + this.lightning + this.light + this.dark;
            }

        }

        /// <summary>
        /// キャラの行動（歩行速度とか）のステータス。
        /// 移動速度など。
        /// 16Byte
        /// SoA Ok
        /// </summary>
        [Serializable]
        [StructLayout(LayoutKind.Sequential)]
        public struct MoveStatus
        {
            /// <summary>
            /// 通常の移動速度
            /// </summary>
            [Header("通常移動速度")]
            public int moveSpeed;

            /// <summary>
            /// 歩行速度。後ろ歩きも同じ
            /// </summary>
            [Header("歩行速度")]
            public int walkSpeed;

            /// <summary>
            /// ダッシュ速度
            /// </summary>
            [Header("ダッシュ速度")]
            public int dashSpeed;

            /// <summary>
            /// ジャンプの高さ。
            /// </summary>
            [Header("ジャンプの高さ")]
            public int jumpHeight;
        }

        #endregion 構造体定義

        #region 実行時キャラクターデータ関連の構造体定義

        /// <summary>
        /// BaseImfo region - キャラクターの基本情報（HP、MP、位置）
        /// サイズ: 32バイト
        /// 用途: 毎フレーム更新される基本ステータス(ID以外)
        /// SoA OK
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct CharacterBaseInfo
        {
            /// <summary>
            /// 最大体力
            /// </summary>
            public int maxHp;

            /// <summary>
            /// 体力
            /// </summary>
            public int currentHp;

            /// <summary>
            /// 最大魔力
            /// </summary>
            public int maxMp;

            /// <summary>
            /// 魔力
            /// </summary>
            public int currentMp;

            /// <summary>
            /// HPの割合
            /// </summary>
            public int hpRatio;

            /// <summary>
            /// MPの割合
            /// </summary>
            public int mpRatio;

            /// <summary>
            /// 現在位置
            /// </summary>
            public float2 nowPosition;

            /// <summary>
            /// HP/MP割合を更新する
            /// </summary>
            public void UpdateRatios()
            {
                hpRatio = maxHp > 0 ? (currentHp * 100) / maxHp : 0;
                mpRatio = maxMp > 0 ? (currentMp * 100) / maxMp : 0;
            }

            /// <summary>
            /// CharacterBaseDataから基本情報を設定
            /// </summary>
            public CharacterBaseInfo(in CharacterBaseData baseData, Vector2 initialPosition)
            {
                maxHp = baseData.hp;
                maxMp = baseData.mp;
                currentHp = baseData.hp;
                currentMp = baseData.mp;
                hpRatio = 100;
                mpRatio = 100;
                nowPosition = initialPosition;
            }
        }

        /// <summary>
        /// 常に変わらないデータを格納する構造体。
        /// BaseDataとの違いは、初期化以降頻繁に参照する必要があるか。
        /// SoA OK 20Byte
        /// </summary>
        [Serializable]
        [StructLayout(LayoutKind.Sequential)]
        public struct SolidData
        {

            /// <summary>
            /// 攻撃属性を示す列挙型
            /// ビット演算で見る
            /// NPCだけ入れる
            /// なに属性の攻撃をしてくるかというところ
            /// </summary>
            [Header("攻撃属性")]
            public Element attackElement;

            /// <summary>
            /// 弱点属性を示す列挙型
            /// ビット演算で見る
            /// NPCだけ入れる
            /// </summary>
            [Header("弱点属性")]
            public Element weakPoint;

            /// <summary>
            /// キャラの属性というか
            /// 特徴を示す。種類も包括
            /// </summary>
            [Header("キャラクター特徴")]
            public CharacterFeature feature;

            /// <summary>
            /// キャラの階級。<br/>
            /// これが上なほど味方の中で同じ敵をターゲットにしててもお控えしなくて済む、優先的に殴れる。<br/>
            /// あとランク低い味方に命令飛ばしたりできる。
            /// </summary>
            [Header("チーム内での階級")]
            public CharacterRank rank;

            /// <summary>
            /// この数値以上の敵から狙われている相手がターゲットになった場合、一旦次の判断までは待機になる
            /// その次の判断でやっぱり一番ヘイト高ければ狙う。(狙われまくってる相手へのヘイトは下がるので、普通はその次の判断でべつのやつが狙われる)
            /// 様子伺う、みたいなステート入れるか専用で
            /// 一定以上に狙われてる相手かつ、様子伺ってるキャラの場合だけヘイト下げるようにしよう。
            /// </summary>
            [Header("ターゲット上限")]
            [Tooltip("この数値以上の敵から狙われている相手が攻撃対象になった場合、一旦次の判断までは待機になる")]
            public int targetingLimit;
        }

        /// <summary>
        /// 参照頻度が少なく、加えて連続参照されないデータを集めた構造体。
        /// 16byte
        /// </summary>
        public struct CharaColdLog
        {
            /// <summary>
            /// キャラクターのID
            /// </summary>
            public readonly int characterID;

            /// <summary>
            /// キャラクターのハッシュ値を保存しておく。
            /// </summary>
            public int hashCode;

            /// <summary>
            /// 最後に判断した時間。
            /// </summary>
            public float lastJudgeTime;

            /// <summary>
            /// 最後に移動判断した時間。
            /// </summary>
            public float lastMoveJudgeTime;

            public CharaColdLog(SOAStatus status, GameObject gameObject)
            {
                characterID = status.characterID;
                hashCode = gameObject.GetHashCode();
                // 最初はマイナスで10000を入れることですぐ動けるように
                this.lastJudgeTime = -10000;
                this.lastMoveJudgeTime = -10000;
            }

        }

        /// <summary>
        /// 攻撃力のデータ
        /// サイズ: 32バイト
        /// 用途: 戦闘計算時にアクセス
        /// SoA OK
        /// </summary>
        [Serializable]
        [StructLayout(LayoutKind.Sequential)]
        public struct CharacterAtkStatus
        {
            /// <summary>
            /// 各属性の基礎攻撃力
            /// </summary>
            public ElementalStatus atk;

            /// <summary>
            /// 全攻撃力の加算
            /// </summary>
            public int dispAtk;

            /// <summary>
            /// 表示用攻撃力を更新
            /// </summary>
            public void UpdateDisplayAttack()
            {
                dispAtk = atk.ReturnSum();
            }

            /// <summary>
            /// CharacterBaseDataから戦闘ステータスを設定
            /// </summary>
            public CharacterAtkStatus(in CharacterBaseData baseData)
            {
                atk = baseData.baseAtk;
                dispAtk = atk.ReturnSum();
            }
        }

        /// <summary>
        /// 防御力のデータ
        /// サイズ: 32バイト
        /// 用途: 戦闘計算時にアクセス
        /// SoA OK
        /// </summary>
        [Serializable]
        [StructLayout(LayoutKind.Sequential)]
        public struct CharacterDefStatus
        {

            /// <summary>
            /// 各属性の基礎防御力
            /// </summary>
            public ElementalStatus def;

            /// <summary>
            /// 全防御力の加算
            /// </summary>
            public int dispDef;

            /// <summary>
            /// 表示用防御力を更新
            /// </summary>
            public void UpdateDisplayDefense()
            {
                dispDef = def.ReturnSum();
            }

            /// <summary>
            /// CharacterBaseDataから戦闘ステータスを設定
            /// </summary>
            public CharacterDefStatus(in CharacterBaseData baseData)
            {
                def = baseData.baseDef;
                dispDef = def.ReturnSum();
            }
        }

        /// <summary>
        /// StateImfo region - キャラクターの状態情報
        /// サイズ: 16バイト（1キャッシュラインの25%）
        /// 用途: AI判断、状態管理
        /// SoA OK
        /// </summary>
        [Serializable]
        [StructLayout(LayoutKind.Sequential)]
        public struct CharacterStateInfo
        {
            /// <summary>
            /// 現在のキャラクターの所属
            /// </summary>
            public CharacterSide belong;

            /// <summary>
            /// 現在の行動状況
            /// 判断間隔経過したら更新？
            /// 攻撃されたりしたら更新？
            /// あと仲間からの命令とかでも更新していいかも
            /// 
            /// 移動とか逃走でAIの動作が変わる。
            /// 逃走の場合は敵の距離を参照して相手が少ないところに逃げようと考えたり
            /// </summary>
            public ActState actState;

            /// <summary>
            /// バフやデバフなどの現在の効果
            /// </summary>
            public SpecialEffect nowEffect;

            /// <summary>
            /// AIが他者の行動を認識するためのイベントフラグ
            /// 列挙型AIEventFlagType　のビット演算に使う
            /// AIManagerがフラグ管理はしてくれる
            /// </summary>
            public BrainEventFlagType brainEvent;

            /// <summary>
            /// 自分を狙ってる敵の数。
            /// ボスか指揮官は無視でよさそう
            /// 今攻撃してるやつも攻撃を終えたら別のターゲットを狙う。
            /// このタイミングで割りこめるやつが割り込む
            /// あくまでヘイト値を減らす感じで。一旦待機になって、ヘイト減るだけなので殴られたら殴り返すよ
            /// 遠慮状態以外なら遠慮になるし、遠慮中でなお一番ヘイト高いなら攻撃して、その次は遠慮になる
            /// </summary>
            public int targetingCount;

            /// <summary>
            /// 状態をリセット（初期化時用）
            /// </summary>
            public void ResetStates()
            {
                nowEffect = SpecialEffect.なし;
                brainEvent = BrainEventFlagType.None;
            }

            /// <summary>
            /// CharacterBaseDataから状態情報を設定
            /// </summary>
            public CharacterStateInfo(in CharacterBaseData baseData)
            {
                belong = baseData.initialBelong;
                actState = baseData.initialMove;
                nowEffect = SpecialEffect.なし;
                brainEvent = BrainEventFlagType.None;
                targetingCount = 0;
            }
        }


        #endregion キャラクターデータ関連の構造体定義

        #region SoA対象外

        #region 判断関連(Job使用)

        /// <summary>
        /// AIの設定。
        /// 要修正。インスペクタで使うだけならこのままにして変換しようか
        /// </summary>
        [Serializable]
        [StructLayout(LayoutKind.Sequential)]
        public struct BrainSetting
        {

            /// <summary>
            /// 行動関連の設定データ
            /// </summary>
            [Header("行動設定")]
            public BehaviorData[] actCondition;



        }

        /// <summary>
        /// AIの設定。（Jobシステム仕様）
        /// ステータスのCharacterSOAStatusから移植する。
        /// 運用法としてはReadOnlyで、各Jobの時に現在の行動時のBrainSettingと判断間隔を抜いてそれきり
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct BrainDataForJob : IDisposable
        {
            /// <summary>
            /// AIの判断間隔
            /// </summary>
            [Header("判断間隔")]
            public float judgeInterval;

            /// <summary>
            /// AIの移動判断間隔
            /// </summary>
            [Header("移動判断間隔")]
            public float moveJudgeInterval;

            /// <summary>
            /// ActStateを変換した int をキーとして行動のデータを持つ
            /// </summary>
            public NativeHashMap<int, BrainSettingForJob> brainSetting;

            /// <summary>
            /// ステータスのデータからJob用のデータを構築する。
            /// </summary>
            /// <param name="sourceDic"></param>
            /// <param name="judgeInterval"></param>
            public BrainDataForJob(ActStateBrainDictionary sourceDic, float judgeInterval, float moveJudgeInterval)
            {
                // 判断間隔を設定。
                this.judgeInterval = judgeInterval;
                this.moveJudgeInterval = moveJudgeInterval;

                brainSetting = new NativeHashMap<int, BrainSettingForJob>(sourceDic.Count, allocator: Allocator.Persistent);

                foreach ( var item in sourceDic )
                {
                    this.brainSetting.Add((int)item.Key, new BrainSettingForJob(item.Value, allocator: Allocator.Persistent));
                }
            }

            /// <summary>
            /// インターバルをまとめて返す。
            /// </summary>
            /// <returns></returns>
            public float2 GetInterval()
            {
                return new float2(judgeInterval, moveJudgeInterval);
            }

            /// <summary>
            /// 各キャラの設定として保持し続けるのでゲーム終了時に呼ぶ。
            /// </summary>
            public void Dispose()
            {
                foreach ( var item in brainSetting )
                {
                    item.Value.Dispose();
                }

                brainSetting.Dispose();
            }
        }

        /// <summary>
        /// AIの設定。（Jobシステム仕様）
        /// ステータスのCharacterSOAStatusから移植する。
        /// 要改修
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct BrainSettingForJob : IDisposable
        {

            /// <summary>
            /// 行動条件データ
            /// </summary>
            [Header("行動条件データ")]
            public NativeArray<BehaviorData> behaviorSetting;

            /// <summary>
            /// NativeArrayリソースを解放する
            /// </summary>
            public void Dispose()
            {
                if ( this.behaviorSetting.IsCreated )
                {
                    this.behaviorSetting.Dispose();
                }
            }

            /// <summary>
            /// オリジナルのCharacterSOAStatusからデータを明示的に移植
            /// </summary>
            /// <param name="source">移植元のキャラクターブレインステータス</param>
            /// <param name="allocator">NativeArrayに使用するアロケータ</param>
            public BrainSettingForJob(in BrainSetting source, Allocator allocator)
            {

                // 配列を新しく作成
                this.behaviorSetting = source.actCondition != null
                    ? new NativeArray<BehaviorData>(source.actCondition, allocator)
                    : new NativeArray<BehaviorData>(0, allocator);
            }

        }

        /// <summary>
        /// ヘイトの設定。（Jobシステム仕様）
        /// ステータスから移植する。
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct HateSettingForJob
        {
            /// <summary>
            /// 攻撃以外の行動条件データ.
            /// 最初の要素ほど優先度高いので重点。
            /// </summary>
            [Header("ヘイト条件データ")]
            public NativeArray<TargetJudgeData> hateCondition;

            public HateSettingForJob(TargetJudgeData[] hateSetting)
            {
                hateCondition = new NativeArray<TargetJudgeData>(hateSetting, Allocator.Persistent);
            }
        }

        /// <summary>
        /// 行動判断時に使用するデータ。
        /// 修正不要:消してそれぞれを配列で持つようにすればOK
        /// </summary>
        [Serializable]
        [StructLayout(LayoutKind.Sequential)]
        public struct BehaviorData
        {
            /// <summary>
            /// 行動をスキップするための条件。
            /// </summary>
            [TabGroup(group: "AI挙動", tab: "スキップ条件")]
            public SkipJudgeData skipData;

            /// <summary>
            /// 行動の条件。
            /// 対象の陣営と特徴を指定できる。
            /// </summary>
            [TabGroup(group: "AI挙動", tab: "行動条件")]
            public ActJudgeData actCondition;

            /// <summary>
            /// 攻撃含むターゲット選択データ
            /// 要素は一つだが、その代わり複雑な条件で指定可能
            /// 特に指定ない場合のみヘイトで動く
            /// ここでヘイト以外の条件を指定した場合は、行動までセットで決める。
            /// </summary>
            [TabGroup(group: "AI挙動", tab: "対象選択条件")]
            public TargetJudgeData targetCondition;
        }

        /// <summary>
        /// 判断に使用するデータ。
        /// SoA OK
        /// 12Byte
        /// </summary>
        [Serializable]
        [StructLayout(LayoutKind.Sequential)]
        public struct SkipJudgeData
        {
            /// <summary>
            /// 行動判定をスキップする条件
            /// </summary>
            [Header("行動判定をスキップする条件")]
            public SkipJudgeCondition skipCondition;

            /// <summary>
            /// 判断に使用する数値。
            /// 条件によってはenumを変換した物だったりする。
            /// </summary>
            [Header("基準となる値")]
            public int judgeValue;

            /// <summary>
            /// 真の場合、条件が反転する
            /// 以上は以内になるなど
            /// </summary>
            [Header("基準反転フラグ")]
            public BitableBool isInvert;

        }

        /// <summary>
        /// 判断に使用するデータ。
        /// 56Byte
        /// SoA OK
        /// </summary>
        [Serializable]
        [StructLayout(LayoutKind.Sequential)]
        public struct ActJudgeData
        {
            /// <summary>
            /// 行動条件
            /// </summary>
            [Header("行動判定の条件")]
            public ActJudgeCondition judgeCondition;

            /// <summary>
            /// 判断に使用する数値。
            /// 条件によってはenumを変換した物だったりする。
            /// </summary>
            [Header("基準となる値")]
            public int judgeValue;

            /// <summary>
            /// 真の場合、条件が反転する
            /// 以上は以内になるなど
            /// </summary>
            [Header("基準反転フラグ")]
            public BitableBool isInvert;

            /// <summary>
            /// これが指定なし、以外だとステート変更を行う。
            /// よって行動判断はスキップ
            /// </summary>
            [Header("変更先のモード（変更する場合）")]
            public ActState stateChange;

            /// <summary>
            /// 対象の陣営区分
            /// 複数指定あり
            /// </summary>
            [Header("チェック対象の条件")]
            public TargetFilter filter;
        }

        /// <summary>
        /// 行動判断後、行動のターゲットを選択する際に使用するデータ。
        /// ヘイトでもそれ以外でも構造体は同じ
        /// 52Byte 
        /// SoA OK
        /// </summary>
        [Serializable]
        [StructLayout(LayoutKind.Sequential)]
        public struct TargetJudgeData
        {
            /// <summary>
            /// ターゲットの判断基準。
            /// </summary>
            [Header("ターゲット判断基準")]
            public TargetSelectCondition judgeCondition;

            /// <summary>
            /// 真の場合、条件が反転する
            /// 以上は以内になるなど
            /// </summary>
            [Header("基準反転フラグ")]
            public BitableBool isInvert;

            /// <summary>
            /// 対象の陣営区分
            /// 複数指定あり
            /// </summary>
            [Header("チェック対象の条件")]
            public TargetFilter filter;

            /// <summary>
            /// 使用する行動の番号。
            /// 指定なし ( = -1)の場合は敵の条件から勝手に決める。(ヘイトで決めた場合は-1の指定なしになる)
            /// そうでない場合はここまで設定する。
            /// 
            /// あるいはヘイト上昇倍率になる。
            /// </summary>
            [Header("ヘイト倍率or使用する行動のNo")]
            [Tooltip("行動番号で-1を指定した場合、AIが対象の情報から行動を決める")]
            public float useAttackOrHateNum;
        }

        /// <summary>
        /// 行動条件や対象設定条件で検査対象をフィルターするための構造体
        /// 40Byte
        /// SoA OK
        /// </summary>
        [Serializable]
        [StructLayout(LayoutKind.Sequential)]
        public struct TargetFilter
        {
            /// <summary>
            /// 対象の陣営区分
            /// 複数指定あり
            /// </summary>
            [Header("対象の陣営")]
            [SerializeField]
            private CharacterSide targetType;

            /// <summary>
            /// 対象の特徴
            /// 複数指定あり
            /// </summary>
            [Header("対象の特徴")]
            [SerializeField]
            private CharacterFeature targetFeature;

            /// <summary>
            /// このフラグが真の時、全部当てはまってないとダメ。
            /// </summary>
            [Header("特徴の判断方法")]
            [SerializeField]
            private BitableBool isAndFeatureCheck;

            /// <summary>
            /// 対象の状態（バフ、デバフ）
            /// 複数指定あり
            /// </summary>
            [Header("対象が持つ特殊効果")]
            [SerializeField]
            private SpecialEffect targetEffect;

            /// <summary>
            /// このフラグが真の時、全部当てはまってないとダメ。
            /// </summary>
            [Header("特殊効果の判断方法")]
            [SerializeField]
            private BitableBool isAndEffectCheck;

            /// <summary>
            /// 対象の状態（逃走、攻撃など）
            /// 複数指定あり
            /// </summary>
            [Header("対象の状態")]
            [SerializeField]
            private ActState targetState;

            /// <summary>
            /// 対象のイベント状況（大ダメージを与えた、とか）でフィルタリング
            /// 複数指定あり
            /// </summary>
            [Header("対象のイベント")]
            [SerializeField]
            private BrainEventFlagType targetEvent;

            /// <summary>
            /// このフラグが真の時、全部当てはまってないとダメ。
            /// </summary>
            [Header("イベントの判断方法")]
            [SerializeField]
            private BitableBool isAndEventCheck;

            /// <summary>
            /// 対象の弱点属性でフィルタリング
            /// 複数指定あり
            /// </summary>
            [Header("対象の弱点")]
            [SerializeField]
            private Element targetWeakPoint;

            /// <summary>
            /// 対象が使う属性でフィルタリング
            /// 複数指定あり
            /// </summary>
            [Header("対象の使用属性")]
            [SerializeField]
            private Element targetUseElement;

            /// <summary>
            /// 検査対象キャラクターの条件に当てはまるかをチェックする。
            /// </summary>
            /// <param name="belong"></param>
            /// <param name="feature"></param>
            /// <returns></returns>
            [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
            public byte IsPassFilter(in SolidData solidData, in CharacterStateInfo stateInfo)
            {
                // andかorで特徴条件判定
                // 当てはまらないなら帰る。
                if ( this.isAndFeatureCheck == BitableBool.TRUE ? ((this.targetFeature != 0) && (this.targetFeature & solidData.feature) != this.targetFeature) :
                                                      ((this.targetFeature != 0) && (this.targetFeature & solidData.feature) == 0) )
                {
                    return 0;
                }

                // 特殊効果判断
                // 当てはまらないなら帰る。
                if ( this.isAndEffectCheck == BitableBool.TRUE ? ((this.targetEffect != 0) && (this.targetEffect & stateInfo.nowEffect) != this.targetEffect) :
                                          ((this.targetEffect != 0) && (this.targetEffect & stateInfo.nowEffect) == 0) )
                {
                    return 0;
                }

                // イベント判断
                // 当てはまらないなら帰る。
                if ( this.isAndEventCheck == BitableBool.TRUE ? ((this.targetEvent != 0) && (this.targetEvent & stateInfo.brainEvent) != this.targetEvent) :
                                          ((this.targetEvent != 0) && (this.targetEvent & stateInfo.brainEvent) == 0) )
                {
                    return 0;
                }

                // 残りの条件も判定。
                if ( (this.targetType == 0 || ((this.targetType & stateInfo.belong) > 0)) && (this.targetState == 0 || ((this.targetState & stateInfo.actState) > 0))
                    && (this.targetWeakPoint == 0 || ((this.targetWeakPoint & solidData.weakPoint) > 0)) && (this.targetUseElement == 0 || ((this.targetUseElement & solidData.attackElement) > 0)) )
                {
                    return 1;
                }

                return 0;
            }

        }

        #endregion 判断関連

        #region キャラクター基幹データ（非Job）

        /// <summary>
        /// 送信するデータ、不変の物
        /// 大半ビットでまとめれそう
        /// 空飛ぶ騎士の敵いるかもしれないしタイプは組み合わせ可能にする
        /// 初期化以降では、ステータスバフやデバフが切れた時に元に戻すくらいしかない
        /// Jobシステムで使用しないのでメモリレイアウトは最適化
        /// SOA対象外
        /// </summary>
        [Serializable]
        [StructLayout(LayoutKind.Auto)]
        public struct CharacterBaseData
        {
            /// <summary>
            /// 最大HP
            /// </summary>
            [Header("HP")]
            public int hp;

            /// <summary>
            /// 最大MP
            /// </summary>
            [Header("MP")]
            public int mp;

            /// <summary>
            /// 各属性の基礎攻撃力
            /// </summary>
            [Header("基礎属性攻撃力")]
            public ElementalStatus baseAtk;

            /// <summary>
            /// 各属性の基礎防御力
            /// </summary>
            [Header("基礎属性防御力")]
            public ElementalStatus baseDef;

            /// <summary>
            /// キャラの初期状態。
            /// </summary>
            [Header("最初にどんな行動をするのかの設定")]
            public ActState initialMove;

            /// <summary>
            /// デフォルトのキャラクターの所属
            /// </summary>
            public CharacterSide initialBelong;
        }

        /// <summary>
        /// 攻撃モーションのステータス。
        /// これはダメージ計算用のデータだから、攻撃時移動やエフェクトのデータは他に持つ。
        /// これはステータスのScriptableに持たせておくのでエフェクトデータとかの参照型も入れていい。
        /// 前回使用した時間、とかを記録するために、キャラクター側に別途リンクした管理情報が必要。
        /// あとJobシステムで使用しない構造体はなるべくメモリレイアウトを最適化する。ネイティブコードとの連携を気にしなくていいから。
        /// 実際にゲームに組み込む時は攻撃以外の行動にも対応できるようにするか。
        /// </summary>
        [Serializable]
        [StructLayout(LayoutKind.Auto)]
        public struct AttackData
        {
            /// <summary>
            /// 攻撃倍率。
            /// いわゆるモーション値
            /// </summary>
            [Header("攻撃倍率（モーション値）")]
            public float motionValue;


        }

        #endregion

        #endregion SoA対象外

        #region シリアライズ可能なディクショナリの定義

        /// <summary>
        /// ActStateがキーでCharacterSOAStatusが値のディクショナリ
        /// </summary>
        [Serializable]
        public class ActStateBrainDictionary : SerializableDictionary<ActState, BrainSetting>
        {
        }

        #endregion

        // ここから下で各データを設定。キャラの種類ごとのステータス。

        /// <summary>
        /// キャラのID
        /// </summary>
        public int characterID;

        /// <summary>
        /// AIの判断間隔
        /// </summary>
        [Header("判断間隔")]
        public float judgeInterval;

        /// <summary>
        /// AIの移動判断間隔
        /// </summary>
        [Header("移動判断間隔")]
        public float moveJudgeInterval;

        /// <summary>
        /// キャラのベース、固定部分のデータ。
        /// これは直接仕様はせず、コピーして各キャラに渡してあげる。
        /// </summary>
        [Header("キャラクターの基本データ")]
        public CharacterBaseData baseData;

        /// <summary>
        /// 固定のデータ。
        /// </summary>
        [Header("固定データ")]
        public SolidData solidData;

        /// <summary>
        /// 攻撃以外の行動条件データ.
        /// 最初の要素ほど優先度高いので重点。
        /// </summary>
        [Header("ヘイト条件データ")]
        public TargetJudgeData[] hateCondition;

        /// <summary>
        /// キャラのAIの設定。
        /// モード（攻撃や逃走などの状態）ごとにモードのEnumを int 変換した数をキーにしたHashMapになる。
        /// ActStateBrainDictionaryはシリアライズ可能なDictionary。
        /// </summary>
        [Header("キャラAIの設定")]
        public ActStateBrainDictionary brainData;

        /// <summary>
        /// 移動速度などのデータ
        /// </summary>
        [Header("移動ステータス")]
        public MoveStatus moveStatus;

        /// <summary>
        /// 各攻撃の攻撃力などの設定。
        /// Jobに入れないので攻撃エフェクト等も持たせていい。
        /// </summary>
        [Header("攻撃データ一覧")]
        public AttackData[] attackData;

        /// <summary>
        /// 旧ステータスのデータをコピーする。
        /// エディターで動かせる関数を用意しよう。
        /// </summary>
        public BrainStatus source;

    }
}

