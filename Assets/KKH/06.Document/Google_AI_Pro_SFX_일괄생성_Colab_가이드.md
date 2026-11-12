# [제작 가이드] Google AI Pro(Colab GPU)를 활용한 보스 효과음(SFX) 대량 일괄 생성 파이프라인

| 문서 속성 | 상세 내용 |
| :--- | :--- |
| **프로젝트명** | 2026_Com2us_Project (RealSteel 2D Action) |
| **활용 자원** | **Google AI Pro** 구독에 포함된 **Google Colab GPU (월 200 컴퓨트 유닛)** |
| **사용 모델** | **TangoFlux** (`declare-lab/TangoFlux`, 44.1kHz 스테레오 오픈소스 SFX 생성 AI) |
| **대상 에셋** | 지역 1 보스 '정크 휠러(호이루)' 전체 효과음 **42종 × 베리에이션** (및 향후 보스 확장 가능) |
| **작성일** | 2026-11-11 |

---

## 1. 왜 이 방법을 쓰는가? (대량 SFX 생산 전략)

Google AI Pro 요금제로 **대량의 효과음(SFX)을 빠르게 많이** 뽑아야 할 때 가장 효율적인 구성은 **Google Colab(L4 또는 T4 GPU) + `TangoFlux`** 조합입니다.

* **크레딧 가성비**: Veo 3로 영상을 매번 렌더링하면 크레딧 소모가 크고 추출도 번거롭지만, **TangoFlux**는 Colab GPU에서 **음원 1개당 약 3~4초** 만에 생성되며 AI Pro에 기본 포함된 **월 200 컴퓨트 유닛** 중 극소량(전체 60~90개 생성 시 약 1~2유닛)만 사용합니다.
* **확장성**: 파이썬 리스트에 2보스, 3보스, 플레이어 파츠 피격음, UI 효과음 프롬프트만 추가하면 똑같이 한 번에 수백 개의 `.wav` 파일을 `.zip`으로 뽑아낼 수 있습니다.

---

## 2. Google Colab 실행 절차 (복붙 2단계)

1. **[Google Colab](https://colab.research.google.com/)** 접속 후 `[새 노트]` 생성 (AI Pro 계정 로그인 필수).
2. 상단 메뉴 **[런타임] ➔ [런타임 유형 변경] ➔ 하드웨어 가속기 `L4 GPU` (또는 `T4 GPU`) 선택**.
3. 아래 **셀 1**과 **셀 2**를 순서대로 붙여넣고 실행하면, 호이루(정크 휠러) 효과음 전체가 `.wav` 파일로 생성된 뒤 `Boss1_JunkWheeler_SFX.zip` 파일로 자동 다운로드됩니다.

---

### [셀 1] TangoFlux 및 오디오 유틸리티 설치 (최신 Colab PyTorch 호환 버전)

> **주의**: `TangoFlux` 원본 설치 파일이 구버전 `torch==2.4.0`을 강제하고 있어 최신 Colab에서 버전 충돌 에러가 발생합니다. 따라서 **`--no-deps` 옵션**으로 Colab에 이미 설치된 최신 PyTorch/CUDA를 유지한 채 설치합니다.

```python
# 1. Colab 기본 PyTorch를 건드리지 않고 필수 의존성만 먼저 설치
!pip install -q diffusers transformers accelerate datasets safetensors huggingface_hub pyyaml tqdm soundfile scipy librosa torchlibrosa

# 2. torch==2.4.0 강제 체크를 우회하여 TangoFlux 본체만 설치
!pip install -q --no-deps git+https://github.com/declare-lab/TangoFlux

import torch
print("PyTorch 버전:", torch.__version__)
print("GPU 사용 가능 여부:", torch.cuda.is_available(), torch.cuda.get_device_name(0) if torch.cuda.is_available() else "CPU")
```

---

### [셀 1.5 - 사전 체크] Hugging Face 로그인 (401 에러 방지)

`TangoFlux`가 내부 오디오 디코더(`stabilityai/stable-audio-open-1.0`의 VAE)를 받을 때 Hugging Face 계정의 **무료 약관 동의 및 Read 토큰**이 필요합니다. *(토큰 입력이 번거로우시면 문서 맨 아래 `4. 토큰 없는 대안 (AudioLDM 2)` 코드를 바로 쓰셔도 됩니다.)*

1. [huggingface.co/stabilityai/stable-audio-open-1.0](https://huggingface.co/stabilityai/stable-audio-open-1.0) 접속 후 로그인 ➔ **`Agree / Access Repository`** 클릭
2. [huggingface.co/settings/tokens](https://huggingface.co/settings/tokens) 에서 **`Read`** 권한 토큰(`hf_...`) 생성 후 복사
3. 아래 셀 2 시작 부분의 `login()` 입력창에 토큰 붙여넣기 (1회만 하면 됨)

---

### [셀 2] 호이루(정크 휠러) 42종 효과음 × 베리에이션 일괄 생성 & ZIP 다운로드

> **베리에이션 개수 조절**: 아래 코드의 `VARIATIONS_PER_CUE = 2` 값을 `3`으로 바꾸면 각 효과음마다 시드가 다른 3개 버전(`_v1`, `_v2`, `_v3`)을 생성하여 총 **126개**의 사운드를 한 번에 얻을 수 있습니다. (기본 `2` 설정 시 **84개** 생성)

```python
import os
import zipfile
import numpy as np
import soundfile as sf
import torch
from huggingface_hub import login
from tangoflux import TangoFluxInference

# Hugging Face 토큰 로그인 (이미 로그인되어 있거나 public 캐시 상태면 스킵 가능)
login()

# =========================================================
# 1. 설정 (원하는 베리에이션 수 및 스텝 수 조절)
# =========================================================
OUTPUT_DIR = "Boss1_JunkWheeler_SFX"
ZIP_FILENAME = "Boss1_JunkWheeler_SFX.zip"
VARIATIONS_PER_CUE = 2   # 각 효과음당 생성할 베리에이션 수 (추천: 2~3)
STEPS = 50               # 생성 품질 (기본 25, 고품질 권장 50)
SAMPLE_RATE = 44100

os.makedirs(OUTPUT_DIR, exist_ok=True)

print("TangoFlux 모델 로딩 중...")
model = TangoFluxInference(name="declare-lab/TangoFlux")

# =========================================================
# 2. 호이루(정크 휠러) 전체 42종 SFX 큐 리스트 (Cue ID, 프롬프트, 길이(초))
#    -> 향후 2보스/3보스/플레이어 효과음 추가 시 이 리스트에 계속 추가하면 됩니다.
# =========================================================
SFX_CUES = [
    # --- [상시 / 이동] ---
    ("SFX_JW_Idle_Loop", "Heavy diesel engine idling with a deep low mechanical rumble and subtle valve rattle, seamless industrial sound, no music", 6),
    ("SFX_JW_Roll_Move", "Massive tractor rubber tire slowly rolling over coarse gravel and scrap metal with heavy weight friction, no music", 5),

    # --- [패턴 1: 고속 횡단 돌진] ---
    ("SFX_JW_Charge_Tell", "Violent tire burnout screech accelerating into an extreme high-RPM diesel engine revving buildup, tense mechanical scream, no music", 3),
    ("SFX_JW_Charge_Rush", "Massive armored wheel vehicle launching at extreme speed, roaring diesel engine and tearing dirt road whoosh, no music", 3),
    ("SFX_JW_Charge_HitPlayer", "Devastating heavy metal crunch impact as a giant iron machine crashes into steel armor, deep sub punch, no music", 2),
    ("SFX_JW_Charge_WallSlam", "Massive heavy metal wheel violently slamming into concrete wall, explosive smash, crumbling debris and metallic shockwave, no music", 3),
    ("SFX_JW_Charge_Rebound", "Heavy elastic rebound sound, giant thick rubber tire bouncing off solid concrete with resonant low thud, no music", 2),
    ("SFX_JW_Charge_DecelRoll", "Heavy rubber tire rolling to a stop on asphalt with a short screeching brake friction, no music", 2),
    ("SFX_JW_Charge_Interrupted", "Massive diesel engine abruptly stalling and failing with a sputtering cough, metallic gear lock clunk and depressurizing air hiss, no music", 3),
    ("SFX_JW_Charge_FireTrail", "Trail of blazing gasoline fire violently igniting along the ground, roaring crackling flames loop, no music", 4),

    # --- [패턴 2: 3단 내려찍기 & 못판 펑크] ---
    ("SFX_JW_Slam_Launch", "Powerful pneumatic piston release launching a heavy multi-ton machine straight into the air, forceful air burst whoosh, no music", 2),
    ("SFX_JW_Slam_Hover", "Massive spinning wheel suspended in midair cutting through wind with heavy whipping whoosh and deep mechanical drone, no music", 3),
    ("SFX_JW_Slam_Drop", "Fast plunging dive sound effect, heavy iron object descending rapidly from the air with sharp downward wind friction, no music", 2),
    ("SFX_JW_Slam_Impact", "Catastrophic ground slam as a heavy iron wheel crashes into dirt floor, massive sub bass shockwave and rock debris, no music", 3),
    ("SFX_JW_Nail_Puncture", "Sharp iron spikes viciously puncturing and tearing through a thick heavy rubber tire, loud tearing snap crunch, no music", 2),
    ("SFX_JW_Tire_Blowout", "Violent high-pressure tire blowout explosion followed by rapid depressurizing pressurized gas air hiss, no music", 3),

    # --- [패턴 3: 바이킹 반원 탄막] ---
    ("SFX_JW_Barrage_Jump", "Medium pneumatic hydraulic thrust launching an armored robot into an aerial arc, mechanical jump whoosh, no music", 2),
    ("SFX_JW_Barrage_Shot", "Punchy pneumatic rivet gun shot, air-powered metal cannon firing a single steel bolt, crisp mechanical click pop, no music", 1),
    ("SFX_JW_Barrage_Fly", "Sharp spinning metal scrap projectile whizzing rapidly through the air, ricochet flyby whistling, no music", 2),
    ("SFX_JW_Barrage_Hit", "High-velocity metal slug crashing into steel armor plate with sharp metallic clink and small explosive crack, no music", 2),
    ("SFX_JW_Barrage_Land", "Heavy mechanical wheel landing firmly on the floor with a solid rubber thud and chassis creak, no music", 2),

    # --- [패턴 4: 회전 톱날 당구 돌진] ---
    ("SFX_JW_Blade_Deploy", "Spring-loaded heavy steel circular saw blades violently snapping out of armored chassis, sharp mechanical latch clank, no music", 2),
    ("SFX_JW_Blade_Spin_Loop", "Industrial heavy circular buzzsaw spinning at hyper speed, screaming metal friction whine cutting the air, no music", 4),
    ("SFX_JW_Blade_Bounce", "High-speed spinning saw blade violently ricocheting off a metal wall, explosive electric sparks and sharp metallic ping, no music", 2),
    ("SFX_JW_Blade_SlashHit", "Vicious circular saw blade grinding deep into steel armor plating, spraying sparks with gnashing metal crunch, no music", 2),
    ("SFX_JW_Blade_Retract", "Rotating metal saw blade slowing down and sliding back into armored chassis with a solid mechanical lock clack, no music", 2),

    # --- [패턴 5: 드리블 충격파 (1~4단계 점진 확장)] ---
    ("SFX_JW_Dribble_TellCrack", "Deep tectonic rumble as solid stone ground fractures and tears open, low frequency earthquake crack sound, no music", 2),
    ("SFX_JW_Dribble_Bounce_1", "Heavy rubber tire bouncing once on solid ground with a compact thud and subtle dirt crunch, no music", 2),
    ("SFX_JW_Dribble_Bounce_2", "Heavy mechanical bounce slam on ground with expanding dirt dust and resonant metallic thud, no music", 2),
    ("SFX_JW_Dribble_Bounce_3", "Massive ground slam bounce with strong seismic shock, shattering rock fragments and heavy weight impact, no music", 2),
    ("SFX_JW_Dribble_Bounce_4", "Colossal seismic ground-shattering explosion impact, massive sub-bass shockwave tearing through battlefield, no music", 3),
    ("SFX_JW_Dribble_Shockwave", "Fast-moving seismic earth wave rolling across the ground, churning stones, dust and tearing dirt rumble, no music", 3),
    ("SFX_JW_Dribble_SteamExhaust", "Loud pressure-release valve hiss, high-pressure steam violently exhausting from an overheated diesel engine, no music", 2),

    # --- [보스 피격 / 상태 변화 / 사망] ---
    ("SFX_JW_Hit_Armor", "Heavy blunt hit against a thick scrap steel armor plate, resonant dull iron clank, no music", 1),
    ("SFX_JW_Hit_Rubber", "Solid dull deep punch hitting a thick heavy rubber tire, damp low frequency thud, no music", 1),
    ("SFX_JW_Tire_Break_Final", "Violent tire shredding apart and bare iron wheel rim brutally screeching and grinding against concrete with sparks, no music", 4),
    ("SFX_JW_State_Groggy", "Diesel engine struggling, sputtering irregular mechanical cough and declining electronic power-down whine, no music", 4),
    ("SFX_JW_Phase2_Roar", "Menacing mechanical roar as diesel engine enters overdrive, afterburner flame ignition burst and deep combustion rumble, no music", 4),
    ("SFX_JW_Defeat_Explosion", "Massive scrap metal robot catastrophically exploding inside, secondary blasts followed by heavy metal debris collapse, no music", 5),

    # --- [필드 기믹 3종] ---
    ("SFX_Field_NailBoard_Rise", "Heavy iron bear trap spikes violently springing up with sharp metallic snap clack, no music", 2),
    ("SFX_Field_ExplosiveBox_Tick", "Urgent fast electronic bomb countdown warning beep ticking, no music", 2),
    ("SFX_Field_ExplosiveBox_Blast", "Industrial dynamite explosive crate detonation, loud fiery demolition blast boom, no music", 3),
]

def normalize_audio(arr, target_peak=0.92):
    max_val = np.max(np.abs(arr))
    if max_val > 1e-6:
        arr = arr * (target_peak / max_val)
    return arr

total_files = len(SFX_CUES) * VARIATIONS_PER_CUE
current = 0

for cue_id, prompt, dur in SFX_CUES:
    for v in range(1, VARIATIONS_PER_CUE + 1):
        current += 1
        seed = current * 1337
        torch.manual_seed(seed)
        if torch.cuda.is_available():
            torch.cuda.manual_seed_all(seed)

        print(f"[{current}/{total_files}] 생성 중: {cue_id}_v{v} ({dur}s)...")
        audio = model.generate(prompt, steps=STEPS, duration=dur)

        # (channels, samples) -> (samples, channels) 변환 및 피크 노멀라이즈
        arr = audio.detach().cpu().float().numpy()
        if arr.ndim == 2 and arr.shape[0] <= 2:
            arr = arr.T
        arr = normalize_audio(arr)

        out_path = os.path.join(OUTPUT_DIR, f"{cue_id}_v{v}.wav")
        sf.write(out_path, arr, SAMPLE_RATE)

# ZIP 압축 및 Colab 다운로드
with zipfile.ZipFile(ZIP_FILENAME, 'w', zipfile.ZIP_DEFLATED) as zf:
    for root, _, files in os.walk(OUTPUT_DIR):
        for file in sorted(files):
            zf.write(os.path.join(root, file), file)

print(f"\n완료! 총 {total_files}개 효과음 생성 및 {ZIP_FILENAME} 압축 완료!")

try:
    from google.colab import files
    files.download(ZIP_FILENAME)
except ImportError:
    print("로컬 환경에서는 현재 폴더의 ZIP 파일을 확인하세요.")
```

---

## 3. 생성 후 유니티 적용 전 2분 후가공 팁

AI가 생성한 효과음 앞뒤에는 0.1~0.3초가량의 미세한 정적(Silence)이 포함될 수 있습니다. 특히 **타격음(`Hit`, `Shot`, `Impact`)** 앞부분에 공백이 있으면 게임에서 프레임과 소리가 어긋나(지연) 느껴집니다.

* 생성된 `.zip` 파일을 프로젝트에 가져오시기 전에 **Audacity** 또는 **FFmpeg/Python 일괄 무음 제거 스크립트**로 앞부분 공백(`Trim Start Silence`)만 잘라내어 유니티에 넣으시면 즉각적인 타격감을 얻을 수 있습니다.
* *(필요 시 다운로드받은 압축 해제 폴더 경로만 알려주시면 앞뒤 무음 자동 컷팅 + 페이드아웃 스크립트를 로컬에서 바로 실행해 드립니다.)*

---

## 4. (대안/개선판) 토큰 없이 멜로디 배제 + 과장 폴리 특화 (`AudioLDM2-Large`)

기존 `AudioLDM 2` 코드에서 **음악/멜로디가 섞이고 밋밋하게 나오던 4가지 원인**을 모두 제거한 개선 버전입니다:
1. **음악 유발 단어(`no music`, `drone`, `scream`, `loop`) 완전 제거**: 긍정 프롬프트에 `music` 단어가 포함되면 오히려 음악 어텐션이 켜지므로 삭제하고 `Close-up foley recording`, `exaggerated punchy transient`, `dry acoustics`로 교체했습니다.
2. **네거티브 프롬프트에서 `distortion` 제거 & 악기/멜로디 집중 차단**: `distortion`을 막으면 엔진/폭발/쇳소리 질감까지 약해지므로 이를 빼고, `melody, chords, synthesizer, piano, guitar, drum beat, reverb`를 강하게 차단했습니다.
3. **CLAP 자동 1등 선별 (`num_waveforms_per_prompt=3`) + `audioldm2-large`**: 3개의 후보를 뽑아 프롬프트와 가장 일치하는 1등 음원(`.audios[0]`)만 자동 채택합니다.
4. **자동 마스터링 DSP 내장**: 앞뒤 무음 자동 제거, 40Hz 초저음 노이즈 컷, 새추레이션 컴프레싱(`tanh`) 및 피크 노멀라이징으로 타격감을 과장·보강합니다.

> **💡 시간 절약 팁**: Colab 런타임을 **L4 GPU** (Google AI Pro 포함)로 변경하고, 먼저 `TEST_ONLY = True` 상태로 **핵심 3개만 1분 만에 뽑아 들어보신 뒤**, 마음에 드실 때 `TEST_ONLY = False`로 바꿔 전체 42종을 실행하세요!

```python
# [멜로디 배제 + 과장 폴리 특화 & GPT2Model/버전 충돌 패치 버전]
# 주의: 이전 셀에서 transformers를 실행한 적 있다면 실행 후 [런타임 -> 세션 다시 시작] 후 실행하세요.
!pip install -q "diffusers>=0.31.0" "transformers==4.48.3" accelerate soundfile scipy

import os, zipfile, torch
import numpy as np
import soundfile as sf
from scipy.signal import butter, sosfilt
from transformers import GPT2LMHeadModel
from diffusers import AudioLDM2Pipeline
from IPython.display import Audio, display

# =========================================================
# 1. 실행 설정 (처음엔 TEST_ONLY = True로 3개만 빠르게 테스트 권장!)
# =========================================================
TEST_ONLY = True   # True: 핵심 3개만 1분 테스트 / False: 전체 42종 생성
MODEL_ID = "cvssp/audioldm2-large"
STEPS = 140
GUIDANCE = 4.5
NUM_CANDIDATES = 3 # 한 큐당 3개 생성 후 CLAP 1등(.audios[0]) 자동 선별
SAMPLE_RATE = 16000

OUT_DIR = "Boss1_Foley_TEST" if TEST_ONLY else "Boss1_Foley_SFX"
ZIP_NAME = "Boss1_Foley_TEST.zip" if TEST_ONLY else "Boss1_Foley_SFX.zip"
os.makedirs(OUT_DIR, exist_ok=True)

print(f"{MODEL_ID} 로딩 중 (GPT2LMHeadModel 패치 적용)...")
# cvssp/audioldm2-large 설정 파일이 구형 GPT2Model로 되어 있는 버그를 직접 우회
lm = GPT2LMHeadModel.from_pretrained(MODEL_ID, subfolder="language_model", torch_dtype=torch.float16)
pipe = AudioLDM2Pipeline.from_pretrained(MODEL_ID, language_model=lm, torch_dtype=torch.float16).to("cuda")

# 악기/멜로디/화음/잔향만 철저히 차단 (거친 금속/엔진 질감을 위해 distortion은 제외!)
NEGATIVE_PROMPT = (
    "music, melody, tune, chords, harmony, rhythm, song, beat, drum beat, "
    "instruments, piano, guitar, synthesizer, orchestra, bassline, singing, vocals, "
    "speech, voice, chatter, ambient pad, heavy reverb, hall echo, room tone"
)

def wrap_foley(desc):
    return (
        f"Close-up foley recording of {desc}, "
        "exaggerated punchy transient, raw isolated physical sound effect, dry studio acoustics"
    )

# 멜로디 유발 단어(loop, drone, scream, no music)를 100% 배제한 물리 묘사 42종
FOLEY_CUES = [
    ("SFX_JW_Idle_Loop", "a heavy old diesel tractor engine idling roughly with mechanical piston clatter and rattle", 4.0),
    ("SFX_JW_Roll_Move", "a giant heavy rubber tractor tire slowly rolling over coarse gravel and steel plates", 4.0),
    ("SFX_JW_Charge_Tell", "car tires violently burning out and screeching on concrete while a heavy diesel engine revs hard", 3.0),
    ("SFX_JW_Charge_Rush", "a massive heavy armored vehicle roaring past at full speed with tearing dirt and wind blast", 3.0),
    ("SFX_JW_Charge_HitPlayer", "a brutal violent car crash smashing straight into thick steel plates with heavy crunch", 2.5),
    ("SFX_JW_Charge_WallSlam", "a giant iron wrecking ball slamming hard into a concrete wall with crumbling rock debris", 3.0),
    ("SFX_JW_Charge_Rebound", "a huge thick rubber tire bouncing forcefully off a solid concrete floor with heavy thud", 2.5),
    ("SFX_JW_Charge_DecelRoll", "heavy rubber truck tires screeching sharply to a sudden brake stop on asphalt", 2.5),
    ("SFX_JW_Charge_Interrupted", "a large diesel engine sputtering, choking, coughing and abruptly shutting down with metallic clunk", 3.0),
    ("SFX_JW_Charge_FireTrail", "intense roaring gasoline fire flames bursting and crackling fiercely along the ground", 3.5),
    ("SFX_JW_Slam_Launch", "a powerful pneumatic hydraulic air piston firing loudly, blasting compressed air", 2.5),
    ("SFX_JW_Slam_Hover", "a massive spinning metal wheel whipping violently through open air with strong wind rush", 3.0),
    ("SFX_JW_Slam_Drop", "a heavy steel weight falling rapidly downward through the air with sharp wind whoosh", 2.5),
    ("SFX_JW_Slam_Impact", "a colossal boulder and anvil slamming violently onto dirt ground, shattering rocks and earth", 3.0),
    ("SFX_JW_Nail_Puncture", "sharp steel nails violently piercing and tearing straight into a thick inflated rubber tire", 2.5),
    ("SFX_JW_Tire_Blowout", "an explosive high-pressure truck tire blowout bang followed by loud pressurized air hissing out", 3.0),
    ("SFX_JW_Barrage_Jump", "a short powerful hydraulic piston thrust punching upward with mechanical snap", 2.5),
    ("SFX_JW_Barrage_Shot", "a single punchy pneumatic nail gun firing a heavy steel rivet sharply", 2.5),
    ("SFX_JW_Barrage_Fly", "a sharp spinning steel blade whizzing fast right past the microphone", 2.5),
    ("SFX_JW_Barrage_Hit", "a heavy steel bullet slug slamming cleanly into a thick iron armor plate with sharp clank", 2.5),
    ("SFX_JW_Barrage_Land", "a heavy armored metal tire landing firmly onto concrete floor with solid thud", 2.5),
    ("SFX_JW_Blade_Deploy", "heavy spring-loaded steel knife blades snapping out and locking tightly with sharp metallic clicks", 2.5),
    ("SFX_JW_Blade_Spin_Loop", "an industrial angle grinder circular saw spinning at high speed and cutting raw steel", 4.0),
    ("SFX_JW_Blade_Bounce", "a spinning metal circular saw violently striking an iron wall and ricocheting with sharp sparks", 2.5),
    ("SFX_JW_Blade_SlashHit", "a heavy buzzsaw viciously grinding and tearing through solid scrap metal plating", 2.5),
    ("SFX_JW_Blade_Retract", "a heavy mechanical steel latch sliding shut and locking firmly with solid iron clank", 2.5),
    ("SFX_JW_Dribble_TellCrack", "solid concrete and rock floor loudly cracking, splitting and tearing apart", 2.5),
    ("SFX_JW_Dribble_Bounce_1", "a thick heavy rubber tire bouncing once on hard dirt ground with solid thump", 2.5),
    ("SFX_JW_Dribble_Bounce_2", "a very heavy rubber wheel slamming onto stone ground with heavy dust impact", 2.5),
    ("SFX_JW_Dribble_Bounce_3", "a massive giant iron weight smashing the ground hard with shattering rock debris", 2.5),
    ("SFX_JW_Dribble_Bounce_4", "a colossal earth-shattering demolition blast and ground crater impact", 3.0),
    ("SFX_JW_Dribble_Shockwave", "a violent rockslide and dirt avalanche rushing swiftly across stone ground", 3.0),
    ("SFX_JW_Dribble_SteamExhaust", "a loud industrial steam boiler pressure relief valve violently blasting white steam hiss", 2.5),
    ("SFX_JW_Hit_Armor", "a single heavy sledgehammer hitting a thick rusty steel plate with blunt clang", 2.5),
    ("SFX_JW_Hit_Rubber", "a heavy wooden bat hitting a thick hollow truck rubber tire with dull thud", 2.5),
    ("SFX_JW_Tire_Break_Final", "a shredded rubber tire ripping off while a bare iron rim violently scrapes and grinds concrete", 3.5),
    ("SFX_JW_State_Groggy", "a dying mechanical engine sputtering unevenly, coughing and losing power completely", 3.5),
    ("SFX_JW_Phase2_Roar", "a roaring industrial flamethrower igniting alongside a fierce revving truck exhaust pipe", 3.5),
    ("SFX_JW_Defeat_Explosion", "multiple heavy explosions blasting inside scrap metal followed by collapsing junk metal crash", 4.5),
    ("SFX_Field_NailBoard_Rise", "a heavy iron bear trap snapping shut violently with sharp metallic spring clack", 2.5),
    ("SFX_Field_ExplosiveBox_Tick", "a sharp short mechanical stopwatch ticking rapidly three times", 2.5),
    ("SFX_Field_ExplosiveBox_Blast", "a loud industrial dynamite demolition explosion blasting wood and steel debris", 3.0),
]

def postprocess_foley(wav, sr=16000):
    wav = np.asarray(wav, dtype=np.float32)
    # 1. 40Hz 이하 뭉개진 저주파 DC 노이즈 제거
    sos = butter(2, 40, btype="highpass", fs=sr, output="sos")
    wav = sosfilt(sos, wav)
    # 2. 앞부분 무음 자동 제거 (즉각적인 타격감 위해 최대 피크의 3% 기준 컷, 5ms 프리롤 유지)
    peak = np.max(np.abs(wav))
    if peak > 1e-5:
        active = np.where(np.abs(wav) > peak * 0.03)[0]
        if len(active) > 0:
            start = max(0, active[0] - int(sr * 0.005))
            end = min(len(wav), active[-1] + int(sr * 0.05))
            wav = wav[start:end]
    # 3. 과장된 펀치감/음압을 위한 소프트 새추레이션 컴프레싱 + 피크 노멀라이징
    peak = np.max(np.abs(wav))
    if peak > 1e-6:
        wav = wav / peak
        wav = np.tanh(wav * 1.45)
        wav = wav / np.max(np.abs(wav)) * 0.94
    return wav.astype(np.float32)

target_cues = [FOLEY_CUES[2], FOLEY_CUES[5], FOLEY_CUES[15]] if TEST_ONLY else FOLEY_CUES

for idx, (cue_id, desc, dur) in enumerate(target_cues, 1):
    gen = torch.Generator("cuda").manual_seed(idx * 2026)
    prompt = wrap_foley(desc)
    print(f"[{idx}/{len(target_cues)}] 생성 중 (후보 {NUM_CANDIDATES}개 중 CLAP 베스트 선별): {cue_id}...")
    audio = pipe(
        prompt=prompt,
        negative_prompt=NEGATIVE_PROMPT,
        num_inference_steps=STEPS,
        guidance_scale=GUIDANCE,
        num_waveforms_per_prompt=NUM_CANDIDATES,
        audio_length_in_s=float(dur),
        generator=gen
    ).audios[0]

    processed = postprocess_foley(audio, SAMPLE_RATE)
    out_path = os.path.join(OUT_DIR, f"{cue_id}.wav")
    sf.write(out_path, processed, SAMPLE_RATE)
    if TEST_ONLY:
        print(f"▶ {cue_id} 미리듣기:")
        display(Audio(out_path, rate=SAMPLE_RATE))

with zipfile.ZipFile(ZIP_NAME, 'w', zipfile.ZIP_DEFLATED) as zf:
    for f in sorted(os.listdir(OUT_DIR)):
        zf.write(os.path.join(OUT_DIR, f), f)

print(f"\n완료! {ZIP_NAME} 자동 다운로드 시작...")
try:
    from google.colab import files
    files.download(ZIP_NAME)
except Exception as e:
    print("자동 다운로드 실패 시 좌측 폴더 아이콘에서 직접 다운로드하세요:", ZIP_NAME)
```

---

## 5. (추가 팩) V8 엔진 공회전 · 드리프트 휠 스핀 · 기계음 10종 전용 생성 코드

방금 성공한 **`AudioLDM2-Large` + `GPT2LMHeadModel` 패치 + 멜로디 배제 파이프라인** 위에, 호이루(정크 휠러)의 메카니컬 감성을 극대화하는 **V8 엔진 / 드리프트 타이어 / 기어·유압·체인 기계음 10종**만 별도로 추가 생성하는 코드입니다.

* **기존 세션 재사용 최적화**: 방금 사운드를 뽑은 Colab 창에서 **새 코드 셀(`+ 코드`)**을 열어 그대로 실행하면 **이미 로드된 `pipe` 모델을 재사용하여 대기 시간 없이 즉시 생성**합니다. *(새 창에서 단독 실행할 때도 자동으로 설치 및 로드됩니다.)*
* **지속형(공회전/드리프트) vs 단발형(기어/유압/백파이어) 자동 분리 후처리**: 공회전·휠 스핀처럼 길게 이어지는 소리는 중간이 잘리지 않게 30ms 부드러운 페이드만 적용하고, 기어 변속·백파이어 같은 단발음은 앞뒤 무음을 타이트하게 컷팅합니다.

```python
# [추가 팩: V8 엔진 공회전 + 드리프트 휠 스핀 + 기계 작동음 10종]
# 이미 직전 셀에서 모델을 로드해 둔 상태라면 재설치/재로딩 없이 바로 생성 시작!
import os, sys, zipfile, subprocess
import numpy as np

if "pipe" not in globals():
    print("새 세션 감지: 필수 패키지 설치 및 AudioLDM2-Large 로딩 중...")
    subprocess.check_call([sys.executable, "-m", "pip", "install", "-q", "diffusers>=0.31.0", "transformers==4.48.3", "accelerate", "soundfile", "scipy"])
    import torch
    from transformers import GPT2LMHeadModel
    from diffusers import AudioLDM2Pipeline
    MODEL_ID = "cvssp/audioldm2-large"
    lm = GPT2LMHeadModel.from_pretrained(MODEL_ID, subfolder="language_model", torch_dtype=torch.float16)
    pipe = AudioLDM2Pipeline.from_pretrained(MODEL_ID, language_model=lm, torch_dtype=torch.float16).to("cuda")
else:
    import torch
    print("기존에 로드된 pipe 모델을 재사용하여 즉시 생성합니다!")

import soundfile as sf
from scipy.signal import butter, sosfilt
from IPython.display import Audio, display

# =========================================================
# 1. 추가 사운드 설정
# =========================================================
VARIATIONS_PER_CUE = 2   # 각 사운드당 생성 개수 (총 10종 x 2 = 20개)
STEPS = 140
GUIDANCE = 4.5
NUM_CANDIDATES = 3       # 3개 생성 후 CLAP 1등(.audios[0]) 자동 선별
SAMPLE_RATE = 16000
PREVIEW_IN_COLAB = True  # Colab 화면에서도 바로 들어볼 수 있게 플레이어 표시

OUT_DIR = "Boss1_Mech_Extra_SFX"
ZIP_NAME = "Boss1_Mech_Extra_SFX.zip"
os.makedirs(OUT_DIR, exist_ok=True)

NEGATIVE_PROMPT = (
    "music, melody, tune, chords, harmony, rhythm, song, beat, drum beat, "
    "instruments, piano, guitar, synthesizer, orchestra, bassline, singing, vocals, "
    "speech, voice, chatter, ambient pad, heavy reverb, hall echo, room tone"
)

def wrap_mech_foley(desc):
    return (
        f"Close-up foley recording of {desc}, "
        "exaggerated punchy transient, raw isolated mechanical sound effect, dry studio acoustics"
    )

# (Cue ID, 물리 묘사, 길이(초), 지속형 루프 여부)
EXTRA_MECH_CUES = [
    # --- [V8 엔진 계열] ---
    ("SFX_JW_V8_Idle", "an aggressive American V8 muscle car engine idling heavily at low RPM with deep throaty exhaust rumble and irregular cam lopey vibration", 4.5, True),
    ("SFX_JW_V8_Ignition", "an old heavy V8 diesel hotrod engine cranking its starter motor twice then roaring loudly to life with raw exhaust bark", 3.5, False),
    ("SFX_JW_V8_Rev_Aggressive", "a monstrous modified V8 racing engine violently revving high on throttle blips and snarling aggressively from open exhaust pipes", 3.5, False),
    ("SFX_JW_V8_Backfire", "a tuned V8 exhaust pipe popping loud explosive gunshot-like backfire bangs and crackles on deceleration", 2.5, False),

    # --- [드리프트 & 휠 스핀 계열] ---
    ("SFX_JW_Drift_Screech", "heavy thick rubber car tires violently drifting, sliding sideways and screeching loudly across rough asphalt pavement", 3.5, True),
    ("SFX_JW_Wheel_Spin_Burnout", "a heavy monster truck rubber wheel spinning furiously in place, tearing gravel grit and screeching hot rubber", 3.5, True),

    # --- [기어 / 유압 / 터보 / 체인 등 기계음 계열] ---
    ("SFX_JW_Mech_GearShift", "heavy steel industrial transmission gears grinding briefly and clunking solidly into locked gear with metallic latch snap", 2.5, False),
    ("SFX_JW_Mech_TurboBlowOff", "a large automotive turbocharger wastegate flutter and sharp pressurized air blow-off valve hiss burst", 2.5, False),
    ("SFX_JW_Mech_HydraulicServo", "heavy industrial hydraulic metal pistons and robotic servo motors extending forcefully with pressurized fluid hiss and iron clank", 3.0, False),
    ("SFX_JW_Mech_ChainRatchet", "heavy rusty iron chains rattling violently against steel armor followed by a heavy mechanical winch ratchet cranking tight", 3.0, False),
]

def postprocess_mech(wav, sr=16000, is_continuous=False):
    wav = np.asarray(wav, dtype=np.float32)
    sos = butter(2, 40, btype="highpass", fs=sr, output="sos")
    wav = sosfilt(sos, wav)

    if is_continuous:
        # 공회전/드리프트 등 지속형은 무음 컷 대신 양끝 30ms 부드러운 페이드 처리 (루프 연결감 유지)
        fade_len = min(int(sr * 0.03), len(wav) // 4)
        if fade_len > 0:
            wav[:fade_len] *= np.linspace(0.0, 1.0, fade_len, dtype=np.float32)
            wav[-fade_len:] *= np.linspace(1.0, 0.0, fade_len, dtype=np.float32)
    else:
        # 단발 충격음은 앞뒤 무음 타이트하게 제거 (5ms 프리롤 유지)
        peak = np.max(np.abs(wav))
        if peak > 1e-5:
            active = np.where(np.abs(wav) > peak * 0.03)[0]
            if len(active) > 0:
                start = max(0, active[0] - int(sr * 0.005))
                end = min(len(wav), active[-1] + int(sr * 0.05))
                wav = wav[start:end]

    peak = np.max(np.abs(wav))
    if peak > 1e-6:
        wav = wav / peak
        wav = np.tanh(wav * 1.42)
        wav = wav / np.max(np.abs(wav)) * 0.94
    return wav.astype(np.float32)

total = len(EXTRA_MECH_CUES) * VARIATIONS_PER_CUE
count = 0

for cue_id, desc, dur, is_cont in EXTRA_MECH_CUES:
    for v in range(1, VARIATIONS_PER_CUE + 1):
        count += 1
        seed = 5000 + count * 777
        gen = torch.Generator("cuda").manual_seed(seed)
        prompt = wrap_mech_foley(desc)
        print(f"[{count}/{total}] 생성 중: {cue_id}_v{v} ({'지속형' if is_cont else '단발형'}, {dur}s)...")

        audio = pipe(
            prompt=prompt,
            negative_prompt=NEGATIVE_PROMPT,
            num_inference_steps=STEPS,
            guidance_scale=GUIDANCE,
            num_waveforms_per_prompt=NUM_CANDIDATES,
            audio_length_in_s=float(dur),
            generator=gen
        ).audios[0]

        processed = postprocess_mech(audio, SAMPLE_RATE, is_continuous=is_cont)
        out_path = os.path.join(OUT_DIR, f"{cue_id}_v{v}.wav")
        sf.write(out_path, processed, SAMPLE_RATE)

        if PREVIEW_IN_COLAB and v == 1:
            display(Audio(out_path, rate=SAMPLE_RATE))

with zipfile.ZipFile(ZIP_NAME, 'w', zipfile.ZIP_DEFLATED) as zf:
    for f in sorted(os.listdir(OUT_DIR)):
        zf.write(os.path.join(OUT_DIR, f), f)

print(f"\n완료! 총 {total}개 추가 기계음 생성 및 {ZIP_NAME} 다운로드 시작...")
try:
    from google.colab import files
    files.download(ZIP_NAME)
except Exception as e:
    print("자동 다운로드 실패 시 좌측 폴더 아이콘에서 직접 다운로드하세요:", ZIP_NAME)
```

---

## 6. (원포인트 추가) 흰 연기 내뿜으며 도넛 그리는 드리프트 카 스핀 전용

고주파만 날카롭게 찢어지던 현상(엔진 저음 제거 + `160Hz` 하이패스 + 과도한 `tanh` 클리핑 원인)을 해결하고, **실제 드리프트 카가 아스팔트에서 도넛을 그리며 연기를 뿜는 묵직하고 자연스러운 타이어 스키드 + 배기음 조화**를 생성하는 코드입니다.

* **핵심 변경점**:
  1. **엔진 + 타이어 동시 조화**: 네거티브에서 `engine rumble`을 제거해 묵직한 중저음 바디가 타이어 마찰음을 받쳐주도록 수정 (대신 드리프트 영상 특유의 `crowd cheering, commentary` 차단).
  2. **실제 도넛 주행 키워드 적용**: 스튜디오 인위적 키워드 대신 AudioSet 실사 데이터에 강한 `drift car doing donuts on asphalt, engine revving and continuous tire screech` 사용.
  3. **왜곡 필터 제거**: `160Hz` 하이패스와 `tanh(1.55)` 과잉 드라이브를 빼고 `Guidance 3.8`로 맞춰 귀 아픈 디지털 고주파 아티팩트를 제거했습니다.

```python
# [원포인트 팩: 흰 연기 내뿜는 드리프트 카 도넛 휠 스핀 4종]
import os, sys, zipfile, subprocess
import numpy as np

if "pipe" not in globals():
    print("새 세션 감지: 필수 패키지 설치 및 AudioLDM2-Large 로딩 중...")
    subprocess.check_call([sys.executable, "-m", "pip", "install", "-q", "diffusers>=0.31.0", "transformers==4.48.3", "accelerate", "soundfile", "scipy"])
    import torch
    from transformers import GPT2LMHeadModel
    from diffusers import AudioLDM2Pipeline
    MODEL_ID = "cvssp/audioldm2-large"
    lm = GPT2LMHeadModel.from_pretrained(MODEL_ID, subfolder="language_model", torch_dtype=torch.float16)
    pipe = AudioLDM2Pipeline.from_pretrained(MODEL_ID, language_model=lm, torch_dtype=torch.float16).to("cuda")
else:
    import torch
    print("기존에 로드된 pipe 모델을 재사용하여 즉시 생성합니다!")

import soundfile as sf
from scipy.signal import butter, sosfilt
from IPython.display import Audio, display

OUT_DIR = "Boss1_DriftDonut_SFX"
ZIP_NAME = "Boss1_DriftDonut_SFX.zip"
os.makedirs(OUT_DIR, exist_ok=True)

# 2가지 약간 다른 도넛 뉘앙스를 2개씩 총 4개 생성
DRIFT_PROMPTS = [
    ("SFX_JW_Drift_Donut_A", "A loud modified drift car doing donuts on an asphalt track, roaring engine revving with heavy continuous rubber tire squeal and burnout"),
    ("SFX_JW_Drift_Donut_B", "A powerful rear-wheel drive sports car drifting tightly in circles, thick rubber tires screeching smoothly across pavement with deep exhaust growl"),
]

NEGATIVE = (
    "music, melody, tune, chords, harmony, instruments, piano, guitar, synthesizer, "
    "singing, vocals, speech, crowd cheering, commentary, siren, alarm, whistle, horn"
)

DURATION = 4.5
STEPS = 140
GUIDANCE = 3.8   # 과도한 고주파 아티팩트 방지를 위해 3.8로 자연스럽게 세팅
SAMPLE_RATE = 16000

for base_name, prompt in DRIFT_PROMPTS:
    for v in (1, 2):
        seed = (7000 if base_name.endswith("A") else 8000) + v * 419
        gen = torch.Generator("cuda").manual_seed(seed)
        print(f"생성 중: {base_name}_v{v} ({DURATION}s)...")
        wav = pipe(
            prompt=prompt,
            negative_prompt=NEGATIVE,
            num_inference_steps=STEPS,
            guidance_scale=GUIDANCE,
            num_waveforms_per_prompt=3,
            audio_length_in_s=DURATION,
            generator=gen
        ).audios[0]

        # 35Hz 초저음 DC만 살짝 정리하고, 고주파 왜곡(tanh) 없이 원음 질감 보존
        wav = np.asarray(wav, dtype=np.float32)
        sos = butter(2, 35, btype="highpass", fs=SAMPLE_RATE, output="sos")
        wav = sosfilt(sos, wav)

        fade_len = int(SAMPLE_RATE * 0.04)
        wav[:fade_len] *= np.linspace(0.0, 1.0, fade_len, dtype=np.float32)
        wav[-fade_len:] *= np.linspace(1.0, 0.0, fade_len, dtype=np.float32)

        peak = np.max(np.abs(wav))
        if peak > 1e-6:
            wav = wav / peak * 0.92

        out_path = os.path.join(OUT_DIR, f"{base_name}_v{v}.wav")
        sf.write(out_path, wav.astype(np.float32), SAMPLE_RATE)
        print(f"▶ {base_name}_v{v} 미리듣기:")
        display(Audio(out_path, rate=SAMPLE_RATE))

with zipfile.ZipFile(ZIP_NAME, 'w', zipfile.ZIP_DEFLATED) as zf:
    for f in sorted(os.listdir(OUT_DIR)):
        zf.write(os.path.join(OUT_DIR, f), f)

print(f"\n완료! {ZIP_NAME} 자동 다운로드 시작...")
try:
    from google.colab import files
    files.download(ZIP_NAME)
except Exception as e:
    print("자동 다운로드 실패 시 좌측 폴더 아이콘에서 직접 다운로드하세요:", ZIP_NAME)
```
