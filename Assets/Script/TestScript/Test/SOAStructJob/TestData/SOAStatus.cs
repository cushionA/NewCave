using CharacterController;
using Sirenix.OdinInspector;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using static CharacterController.AIManager;
using static CharacterController.BrainStatus;

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
        [Flags]
        public enum CharacterSide
        {
            プレイヤー = 1 << 0,// 味方
            魔物 = 1 << 1,// 一般的な敵
            その他 = 1 << 2,// それ以外
            指定なし = 0
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
                this.hpRatio = this.maxHp > 0 ? this.currentHp * 100 / this.maxHp : 0;
                this.mpRatio = this.maxMp > 0 ? this.currentMp * 100 / this.maxMp : 0;
            }

            /// <summary>
            /// CharacterBaseDataから基本情報を設定
            /// </summary>
            public CharacterBaseInfo(in CharacterBaseData baseData, Vector2 initialPosition)
            {
                this.maxHp = baseData.hp;
                this.maxMp = baseData.mp;
                this.currentHp = baseData.hp;
                this.currentMp = baseData.mp;
                this.hpRatio = 100;
                this.mpRatio = 100;
                this.nowPosition = initialPosition;
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
                this.characterID = status.characterID;
                this.hashCode = gameObject.GetHashCode();
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
                this.dispAtk = this.atk.ReturnSum();
            }

            /// <summary>
            /// CharacterBaseDataから戦闘ステータスを設定
            /// </summary>
            public CharacterAtkStatus(in CharacterBaseData baseData)
            {
                this.atk = baseData.baseAtk;
                this.dispAtk = this.atk.ReturnSum();
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
                this.dispDef = this.def.ReturnSum();
            }

            /// <summary>
            /// CharacterBaseDataから戦闘ステータスを設定
            /// </summary>
            public CharacterDefStatus(in CharacterBaseData baseData)
            {
                this.def = baseData.baseDef;
                this.dispDef = this.def.ReturnSum();
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
                this.nowEffect = SpecialEffect.なし;
                this.brainEvent = BrainEventFlagType.None;
            }

            /// <summary>
            /// CharacterBaseDataから状態情報を設定
            /// </summary>
            public CharacterStateInfo(in CharacterBaseData baseData)
            {
                this.belong = baseData.initialBelong;
                this.actState = baseData.initialMove;
                this.nowEffect = SpecialEffect.なし;
                this.brainEvent = BrainEventFlagType.None;
                this.targetingCount = 0;
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
            public BehaviorData[] judgeData;

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

                this.brainSetting = new NativeHashMap<int, BrainSettingForJob>(sourceDic.Count, allocator: Allocator.Persistent);

                foreach ( KeyValuePair<ActState, BrainSetting> item in sourceDic )
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
                return new float2(this.judgeInterval, this.moveJudgeInterval);
            }

            /// <summary>
            /// 各キャラの設定として保持し続けるのでゲーム終了時に呼ぶ。
            /// </summary>
            public void Dispose()
            {
                foreach ( KVPair<int, BrainSettingForJob> item in this.brainSetting )
                {
                    item.Value.Dispose();
                }

                this.brainSetting.Dispose();
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
                this.behaviorSetting = source.judgeData != null
                    ? new NativeArray<BehaviorData>(source.judgeData, allocator)
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
                this.hateCondition = new NativeArray<TargetJudgeData>(hateSetting, Allocator.Persistent);
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
        public struct TargetFilter : IEquatable<TargetFilter>
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
                // すべての条件を2つのuint4にパック
                uint4 masks1 = new(
                    (uint)this.targetFeature,
                    (uint)this.targetEffect,
                    (uint)this.targetEvent,
                    (uint)this.targetType
                );

                uint4 values1 = new(
                    (uint)solidData.feature,
                    (uint)stateInfo.nowEffect,
                    (uint)stateInfo.brainEvent,
                    (uint)stateInfo.belong
                );

                uint4 masks2 = new(
                    (uint)this.targetState,
                    (uint)this.targetWeakPoint,
                    (uint)this.targetUseElement,
                    0u
                );

                uint4 values2 = new(
                    (uint)stateInfo.actState,
                    (uint)solidData.weakPoint,
                    (uint)solidData.attackElement,
                    0u
                );

                // AND/OR判定タイプ（最初の3つだけAND/OR切り替え可能）
                bool4 checkTypes1 = new(
                    this.isAndFeatureCheck == BitableBool.TRUE,
                    this.isAndEffectCheck == BitableBool.TRUE,
                    this.isAndEventCheck == BitableBool.TRUE,
                    false // targetTypeは常にOR判定
                );

                // 2つ目のuint4は全てOR判定
                bool4 checkTypes2 = new(false);

                // SIMD演算
                uint4 and1 = masks1 & values1;
                uint4 and2 = masks2 & values2;

                // 条件判定
                bool4 pass1 = EvaluateConditions(masks1, and1, checkTypes1);
                bool4 pass2 = EvaluateConditions(masks2, and2, checkTypes2);

                // すべての条件がtrueかチェック
                return (byte)(math.all(pass1 & pass2) ? 1 : 0);
            }

            /// <summary>
            /// 条件評価をSIMDで実行するヘルパーメソッド
            /// </summary>
            [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
            private static bool4 EvaluateConditions(uint4 masks, uint4 andResults, bool4 isAndCheck)
            {
                bool4 zeroMasks = masks == 0u;
                bool4 andConditions = andResults == masks;
                bool4 orConditions = andResults > 0u;

                // ビット演算で条件選択を実現
                // isAndCheck が true の場合は andConditions、false の場合は orConditions
                bool4 selectedConditions = (isAndCheck & andConditions) | (!isAndCheck & orConditions);

                return zeroMasks | selectedConditions;
            }

            /// <summary>
            /// 個別パラメータを受け取るコンストラクタ
            /// </summary>
            public TargetFilter(
                BrainStatus.CharacterSide targetType,
                BrainStatus.CharacterFeature targetFeature,
                BrainStatus.BitableBool isAndFeatureCheck,
                BrainStatus.SpecialEffect targetEffect,
                BrainStatus.BitableBool isAndEffectCheck,
                BrainStatus.ActState targetState,
                CharacterController.AIManager.BrainEventFlagType targetEvent,
                BrainStatus.BitableBool isAndEventCheck,
                BrainStatus.Element targetWeakPoint,
                BrainStatus.Element targetUseElement)
            {
                this.targetType = (CharacterSide)(int)targetType;
                this.targetFeature = (CharacterFeature)(int)targetFeature;
                this.isAndFeatureCheck = (BitableBool)(int)isAndFeatureCheck;
                this.targetEffect = (SpecialEffect)(int)targetEffect;
                this.isAndEffectCheck = (BitableBool)(int)isAndEffectCheck;
                this.targetState = (ActState)(int)targetState;
                this.targetEvent = (BrainEventFlagType)(int)targetEvent;
                this.isAndEventCheck = (BitableBool)(int)isAndEventCheck;
                this.targetWeakPoint = (Element)(int)targetWeakPoint;
                this.targetUseElement = (Element)(int)targetUseElement;
            }

            #region デバッグ用

            public CharacterSide GetTargetType()
            {
                return this.targetType;
            }

            public bool Equals(TargetFilter other)
            {
                return this.targetType == other.targetType &&
                       this.targetFeature == other.targetFeature &&
                       this.isAndFeatureCheck == other.isAndFeatureCheck &&
                       this.targetEffect == other.targetEffect &&
                       this.isAndEffectCheck == other.isAndEffectCheck &&
                       this.targetState == other.targetState &&
                       this.targetEvent == other.targetEvent &&
                       this.isAndEventCheck == other.isAndEventCheck &&
                       this.targetWeakPoint == other.targetWeakPoint &&
                       this.targetUseElement == other.targetUseElement;
            }

            /// <summary>
            /// デバッグ用のデコンストラクタ。
            /// var (type, feature, isAndFeature, effect, isAndEffect, 
            ///　state, eventType, isAndEvent, weakPoint, useElement) = filter;
            /// </summary>
            /// <param name="targetType"></param>
            /// <param name="targetFeature"></param>
            /// <param name="isAndFeatureCheck"></param>
            /// <param name="targetEffect"></param>
            /// <param name="isAndEffectCheck"></param>
            /// <param name="targetState"></param>
            /// <param name="targetEvent"></param>
            /// <param name="isAndEventCheck"></param>
            /// <param name="targetWeakPoint"></param>
            /// <param name="targetUseElement"></param>
            public void Deconstruct(
    out CharacterSide targetType,
    out CharacterFeature targetFeature,
    out BitableBool isAndFeatureCheck,
    out SpecialEffect targetEffect,
    out BitableBool isAndEffectCheck,
    out ActState targetState,
    out BrainEventFlagType targetEvent,
    out BitableBool isAndEventCheck,
    out Element targetWeakPoint,
    out Element targetUseElement)
            {
                targetType = this.targetType;
                targetFeature = this.targetFeature;
                isAndFeatureCheck = this.isAndFeatureCheck;
                targetEffect = this.targetEffect;
                isAndEffectCheck = this.isAndEffectCheck;
                targetState = this.targetState;
                targetEvent = this.targetEvent;
                isAndEventCheck = this.isAndEventCheck;
                targetWeakPoint = this.targetWeakPoint;
                targetUseElement = this.targetUseElement;
            }

            /// <summary>
            /// IsPassFilterのデバッグ用メソッド。失敗した条件の詳細を返す
            /// </summary>
            public string DebugIsPassFilter(in SolidData solidData, in CharacterStateInfo stateInfo)
            {
                System.Text.StringBuilder failedConditions = new();

                // 1. 特徴条件判定
                if ( this.targetFeature != 0 )
                {
                    bool featureFailed = false;
                    string failureReason = "";

                    if ( this.isAndFeatureCheck == BitableBool.TRUE )
                    {
                        // AND条件：全ての特徴が必要
                        if ( (this.targetFeature & solidData.feature) != this.targetFeature )
                        {
                            featureFailed = true;
                            CharacterFeature missingFeatures = this.targetFeature & ~solidData.feature;
                            failureReason = $"AND条件失敗 - 必要な特徴が不足: {missingFeatures}";
                        }
                    }
                    else
                    {
                        // OR条件：いずれかの特徴が必要
                        if ( (this.targetFeature & solidData.feature) == 0 )
                        {
                            featureFailed = true;
                            failureReason = "OR条件失敗 - 一致する特徴なし";
                        }
                    }

                    if ( featureFailed )
                    {
                        _ = failedConditions.AppendLine($"[特徴条件で失敗]");
                        _ = failedConditions.AppendLine($"  フィールド: targetFeature");
                        _ = failedConditions.AppendLine($"  期待値: {this.targetFeature} (0x{this.targetFeature:X})");
                        _ = failedConditions.AppendLine($"  実際の値: {solidData.feature} (0x{solidData.feature:X})");
                        _ = failedConditions.AppendLine($"  判定方法: {(this.isAndFeatureCheck == BitableBool.TRUE ? "AND" : "OR")}");
                        _ = failedConditions.AppendLine($"  理由: {failureReason}");
                        _ = failedConditions.AppendLine();
                        return failedConditions.ToString();
                    }
                }

                // 2. 特殊効果判断
                if ( this.targetEffect != 0 )
                {
                    bool effectFailed = false;
                    string failureReason = "";

                    if ( this.isAndEffectCheck == BitableBool.TRUE )
                    {
                        // AND条件：全ての効果が必要
                        if ( (this.targetEffect & stateInfo.nowEffect) != this.targetEffect )
                        {
                            effectFailed = true;
                            SpecialEffect missingEffects = this.targetEffect & ~stateInfo.nowEffect;
                            failureReason = $"AND条件失敗 - 必要な効果が不足: {missingEffects}";
                        }
                    }
                    else
                    {
                        // OR条件：いずれかの効果が必要
                        if ( (this.targetEffect & stateInfo.nowEffect) == 0 )
                        {
                            effectFailed = true;
                            failureReason = "OR条件失敗 - 一致する効果なし";
                        }
                    }

                    if ( effectFailed )
                    {
                        _ = failedConditions.AppendLine($"[特殊効果条件で失敗]");
                        _ = failedConditions.AppendLine($"  フィールド: targetEffect");
                        _ = failedConditions.AppendLine($"  期待値: {this.targetEffect} (0x{this.targetEffect:X})");
                        _ = failedConditions.AppendLine($"  実際の値: {stateInfo.nowEffect} (0x{stateInfo.nowEffect:X})");
                        _ = failedConditions.AppendLine($"  判定方法: {(this.isAndEffectCheck == BitableBool.TRUE ? "AND" : "OR")}");
                        _ = failedConditions.AppendLine($"  理由: {failureReason}");
                        _ = failedConditions.AppendLine();
                        return failedConditions.ToString();
                    }
                }

                // 3. イベント判断
                if ( this.targetEvent != 0 )
                {
                    bool eventFailed = false;
                    string failureReason = "";

                    if ( this.isAndEventCheck == BitableBool.TRUE )
                    {
                        // AND条件：全てのイベントが必要
                        if ( (this.targetEvent & stateInfo.brainEvent) != this.targetEvent )
                        {
                            eventFailed = true;
                            BrainEventFlagType missingEvents = this.targetEvent & ~stateInfo.brainEvent;
                            failureReason = $"AND条件失敗 - 必要なイベントが不足: {missingEvents}";
                        }
                    }
                    else
                    {
                        // OR条件：いずれかのイベントが必要
                        if ( (this.targetEvent & stateInfo.brainEvent) == 0 )
                        {
                            eventFailed = true;
                            failureReason = "OR条件失敗 - 一致するイベントなし";
                        }
                    }

                    if ( eventFailed )
                    {
                        _ = failedConditions.AppendLine($"[イベント条件で失敗]");
                        _ = failedConditions.AppendLine($"  フィールド: targetEvent");
                        _ = failedConditions.AppendLine($"  期待値: {this.targetEvent} (0x{this.targetEvent:X})");
                        _ = failedConditions.AppendLine($"  実際の値: {stateInfo.brainEvent} (0x{stateInfo.brainEvent:X})");
                        _ = failedConditions.AppendLine($"  判定方法: {(this.isAndEventCheck == BitableBool.TRUE ? "AND" : "OR")}");
                        _ = failedConditions.AppendLine($"  理由: {failureReason}");
                        _ = failedConditions.AppendLine();
                        return failedConditions.ToString();
                    }
                }

                // 4. 残りの条件（個別チェック）
                List<string> remainingFailures = new();

                // 陣営チェック
                if ( this.targetType != 0 && (this.targetType & stateInfo.belong) == 0 )
                {
                    remainingFailures.Add($"  - targetType: 期待値={this.targetType} (0x{this.targetType:X}), 実際の値={stateInfo.belong} (0x{stateInfo.belong:X})");
                }

                // 状態チェック
                if ( this.targetState != 0 && (this.targetState & stateInfo.actState) == 0 )
                {
                    remainingFailures.Add($"  - targetState: 期待値={this.targetState} (0x{this.targetState:X}), 実際の値={stateInfo.actState} (0x{stateInfo.actState:X})");
                }

                // 弱点チェック
                if ( this.targetWeakPoint != 0 && (this.targetWeakPoint & solidData.weakPoint) == 0 )
                {
                    remainingFailures.Add($"  - targetWeakPoint: 期待値={this.targetWeakPoint} (0x{this.targetWeakPoint:X}), 実際の値={solidData.weakPoint} (0x{solidData.weakPoint:X})");
                }

                // 使用属性チェック
                if ( this.targetUseElement != 0 && (this.targetUseElement & solidData.attackElement) == 0 )
                {
                    remainingFailures.Add($"  - targetUseElement: 期待値={this.targetUseElement} (0x{this.targetUseElement:X}), 実際の値={solidData.attackElement} (0x{solidData.attackElement:X})");
                }

                if ( remainingFailures.Count > 0 )
                {
                    _ = failedConditions.AppendLine($"[その他の条件で失敗]");
                    foreach ( string failure in remainingFailures )
                    {
                        _ = failedConditions.AppendLine(failure);
                    }

                    return failedConditions.ToString();
                }

                // 全条件パスした場合
                return "全ての条件をパスしました";
            }

            /// <summary>
            /// より詳細なビット解析を含むバージョン
            /// </summary>
            /// <param name="solidData"></param>
            /// <param name="stateInfo"></param>
            /// <returns></returns>
            public string DebugIsPassFilterDetailed(in SolidData solidData, in CharacterStateInfo stateInfo)
            {
                string result = this.DebugIsPassFilter(solidData, stateInfo);

                if ( result != "全ての条件をパスしました" )
                {
                    System.Text.StringBuilder details = new();
                    _ = details.AppendLine("=== 詳細なビット解析 ===");

                    // ビットフラグの詳細を表示する補助メソッド
                    void AppendBitDetails<T>(string name, T expected, T actual) where T : System.Enum
                    {
                        _ = details.AppendLine($"\n{name}:");
                        _ = details.AppendLine($"  期待されるフラグ: {System.Enum.GetName(typeof(T), expected)} = {expected}");
                        _ = details.AppendLine($"  実際のフラグ: {System.Enum.GetName(typeof(T), actual)} = {actual}");

                        // 各ビットの状態を表示
                        int expectedInt = System.Convert.ToInt32(expected);
                        int actualInt = System.Convert.ToInt32(actual);

                        for ( int i = 0; i < 32; i++ )
                        {
                            int bitMask = 1 << i;
                            if ( (expectedInt & bitMask) != 0 )
                            {
                                bool hasbit = (actualInt & bitMask) != 0;
                                _ = details.AppendLine($"    ビット{i}: {(hasbit ? "○" : "×")}");
                            }
                        }
                    }

                    // 必要に応じて各フィールドの詳細を追加
                    if ( result.Contains("targetFeature") )
                    {
                        AppendBitDetails("CharacterFeature", this.targetFeature, solidData.feature);
                    }

                    result += "\n" + details.ToString();
                }

                return result;
            }
            #endregion
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

        [ContextMenu("テストデータ複製")]
        public void TestDataCopy()
        {
            this.baseData.hp = this.source.baseData.hp;
            this.baseData.mp = this.source.baseData.mp;

            this.baseData.baseAtk.slash = this.source.baseData.baseAtk.slash;
            this.baseData.baseAtk.pierce = this.source.baseData.baseAtk.pierce;
            this.baseData.baseAtk.strike = this.source.baseData.baseAtk.strike;
            this.baseData.baseAtk.fire = this.source.baseData.baseAtk.fire;
            this.baseData.baseAtk.lightning = this.source.baseData.baseAtk.lightning;
            this.baseData.baseAtk.light = this.source.baseData.baseAtk.light;
            this.baseData.baseAtk.dark = this.source.baseData.baseAtk.dark;

            this.baseData.baseDef.slash = this.source.baseData.baseDef.slash;
            this.baseData.baseDef.pierce = this.source.baseData.baseDef.pierce;
            this.baseData.baseDef.strike = this.source.baseData.baseDef.strike;
            this.baseData.baseDef.fire = this.source.baseData.baseDef.fire;
            this.baseData.baseDef.lightning = this.source.baseData.baseDef.lightning;
            this.baseData.baseDef.light = this.source.baseData.baseDef.light;
            this.baseData.baseDef.dark = this.source.baseData.baseDef.dark;

            this.baseData.initialBelong = (CharacterSide)(int)this.source.baseData.initialBelong;
            this.baseData.initialMove = (ActState)(int)this.source.baseData.initialMove;

            this.solidData.attackElement = (Element)(int)this.source.solidData.attackElement;
            this.solidData.weakPoint = (Element)(int)this.source.solidData.weakPoint;
            this.solidData.feature = (CharacterFeature)(int)this.source.solidData.feature;
            this.solidData.rank = (CharacterRank)(int)this.source.solidData.rank;
            this.solidData.targetingLimit = this.source.solidData.targetingLimit;

            foreach ( KeyValuePair<BrainStatus.ActState, CharacterBrainStatus> item in this.source.brainData )
            {
                BrainSetting setting = new();

                setting.judgeData = new BehaviorData[item.Value.actCondition.Length];
                for ( int i = 0; i < setting.judgeData.Length; i++ )
                {
                    setting.judgeData[i].actCondition.judgeValue = item.Value.actCondition[i].actCondition.judgeValue;
                    setting.judgeData[i].actCondition.judgeCondition = (ActJudgeCondition)(int)item.Value.actCondition[i].actCondition.judgeCondition;
                    setting.judgeData[i].actCondition.stateChange = (ActState)(int)item.Value.actCondition[i].actCondition.stateChange;
                    setting.judgeData[i].actCondition.isInvert = (BitableBool)(int)item.Value.actCondition[i].actCondition.isInvert;

                    (BrainStatus.CharacterSide targetType, BrainStatus.CharacterFeature targetFeature, BrainStatus.BitableBool isAndFeatureCheck, BrainStatus.SpecialEffect targetEffect, BrainStatus.BitableBool isAndEffectCheck,
     BrainStatus.ActState targetState, BrainEventFlagType targetEvent, BrainStatus.BitableBool isAndEventCheck, BrainStatus.Element targetWeakPoint, BrainStatus.Element targetUseElement) = item.Value.actCondition[i].actCondition.filter;
                    setting.judgeData[i].actCondition.filter = new TargetFilter(targetType, targetFeature, isAndFeatureCheck, targetEffect, isAndEffectCheck,
     targetState, targetEvent, isAndEventCheck, targetWeakPoint, targetUseElement);

                    setting.judgeData[i].skipData.skipCondition = (SkipJudgeCondition)(int)item.Value.actCondition[i].skipData.skipCondition;
                    setting.judgeData[i].skipData.judgeValue = item.Value.actCondition[i].skipData.judgeValue;
                    setting.judgeData[i].skipData.isInvert = (BitableBool)(int)item.Value.actCondition[i].skipData.isInvert;

                    setting.judgeData[i].targetCondition.isInvert = (BitableBool)(int)item.Value.actCondition[i].targetCondition.isInvert;

                    setting.judgeData[i].targetCondition.judgeCondition = (TargetSelectCondition)(int)item.Value.actCondition[i].targetCondition.judgeCondition;
                    setting.judgeData[i].targetCondition.isInvert = (BitableBool)(int)item.Value.actCondition[i].targetCondition.isInvert;
                    setting.judgeData[i].targetCondition.useAttackOrHateNum = item.Value.actCondition[i].targetCondition.useAttackOrHateNum;

                    (targetType, targetFeature, isAndFeatureCheck, targetEffect, isAndEffectCheck,
     targetState, targetEvent, isAndEventCheck, targetWeakPoint, targetUseElement) = item.Value.actCondition[i].targetCondition.filter;
                    setting.judgeData[i].targetCondition.filter = new TargetFilter(targetType, targetFeature, isAndFeatureCheck, targetEffect, isAndEffectCheck,
 targetState, targetEvent, isAndEventCheck, targetWeakPoint, targetUseElement);

                }

                this.brainData.Add((ActState)(int)item.Key, setting);
            }

            this.moveStatus.moveSpeed = this.source.moveStatus.moveSpeed;
            this.moveStatus.walkSpeed = this.source.moveStatus.walkSpeed;
            this.moveStatus.dashSpeed = this.source.moveStatus.dashSpeed;
            this.moveStatus.jumpHeight = this.source.moveStatus.jumpHeight;

        }

    }
}

