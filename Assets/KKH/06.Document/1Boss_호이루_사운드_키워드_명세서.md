# [사운드 명세서] 지역 1 보스 '호이루 (정크 휠러)' 사운드 에셋 검색 & 생성 키워드 매칭 매뉴얼

| 문서 속성 | 상세 내용 |
| :--- | :--- |
| **프로젝트명** | 2026_Com2us_Project (RealSteel 2D Action) |
| **대상 보스** | 지역 1 보스 - **정크 휠러 (Junk Wheeler / 별칭: 호이루)** |
| **문서 목적** | 상용 사운드 라이브러리(Freesound, Sonniss, Soundly 등) 검색 및 AI 사운드 생성(ElevenLabs SFX, AudioCraft)을 위한 정밀 키워드 매칭 |
| **사운드 컨셉** | **헤비 디젤 엔진 + 거대 개조 폐타이어 + 회전 톱날(SFX)** / **묵직한 고철 투기장 감성의 헤비메탈·데스메탈(BGM)** |
| **작성일** | 2026-11-11 (BGM 컨셉 메탈로 개편) |

---

## 1. 보스 캐릭터 음향 정체성 (Acoustic Identity)

1. **소재 질감 (Materials)**:
   - **거대 고무 타이어 (Heavy Rubber)**: 트랙터/덤프트럭 규격의 두껍고 질긴 고무. 묵직한 저음(Low Thud), 지면 마찰 스크리치(Tire Screech), 탄성 튕김(Elastic Bounce).
   - **중장비 디젤 엔진 (Heavy Diesel Engine)**: 거친 피스톤 타격음(Piston Clatter), 고회전 배기 굉음(High-RPM Exhaust Roar), 머플러 백파이어(Backfire Pop).
   - **폐고철 프레임 & 강철 회전 톱날 (Scrap Metal & Steel Blades)**: 고속 회전 쇠마찰음(Saw Whine), 강철 격돌 쇳소리(Sharp Clang), 불꽃 스파크(Spark Sizzle).
   - **공압/유압 & 증기 (Pneumatics & Steam)**: 밸브 개방 파열음(Pneumatic Release), 타이어 펑크 바람 빠짐(Air Hiss), 과열 증기(Steam Vent).

2. **레이어드 사운드 원칙 (3-Band Layering Rule)**:
   - **Sub/Low (20~120Hz)**: 차체 무게감, 착지 지진파, 대형 타이어 쿵 소리 (묵직한 타격감 부여)
   - **Mid (200~2.5kHz)**: 디젤 피스톤 작동, 타이어 고무 마찰, 엔진 가속 (소리의 본체/질감 형성)
   - **High (3k~12kHz)**: 톱날 금속 마찰, 브레이크 쇳소리, 스파크, 못판 관통 (플레이어 귀에 박히는 선명한 식별 신호)

---

## 2. 행동 및 패턴별 사운드 키워드 매핑 큐 시트 (Cue Sheet)

### 2.1 상시 / 루프 / 이동 사운드 (Ambience & Movement)

| Cue ID | 상황 / 트리거 시점 | 사운드 묘사 및 레이어링 | 추천 검색 키워드 (Freesound / Sonniss) | AI SFX 생성 프롬프트 (ElevenLabs 등) |
| :--- | :--- | :--- | :--- | :--- |
| `SFX_JW_Idle_Loop` | 보스 대기 (Idle) 상태 무한 루프 | 낡은 디젤 엔진이 덜덜거리며 돌아가는 저회전 공회전음, 배기 밸브의 미세한 칙칙 소리 | `diesel engine idle`, `tractor idle loop`, `heavy truck low rpm rumble`, `mechanical engine hum` | "Heavy diesel engine idling with a deep low mechanical rumble and subtle valve rattle, looping sound effect, real steel industrial feel" |
| `SFX_JW_Roll_Move` | 지면 이동 / 바퀴 굴림 | 묵직한 거대 타이어가 거친 흙/자갈/철판 바닥을 짓이기며 굴러가는 소리 + 내부 체인 유격음 | `heavy tire roll gravel`, `truck tire rolling dirt`, `rubber rolling on concrete`, `chain rattling metal` | "Massive tractor tire slowly rolling over coarse gravel and scrap metal with heavy weight and rubber friction" |

---

### 2.2 [패턴 1] 고속 횡단 돌진 (Charge Attack)

| Cue ID | 상황 / 트리거 시점 | 사운드 묘사 및 레이어링 | 추천 검색 키워드 (Freesound / Sonniss) | AI SFX 생성 프롬프트 (ElevenLabs 등) |
| :--- | :--- | :--- | :--- | :--- |
| `SFX_JW_Charge_Tell` | 전조 시작 (2.0s 동안 지수 가속) | 제자리 바퀴 헛돌림(Burnout) + 엔진 RPM이 점진적으로 폭발하듯 솟구치는 소리 + 임계 직전 고음 경고음 | `car burnout tire screech`, `engine rev up crescendo`, `rev limiter backfire`, `mechanical turbine charge up` | "Violent tire burnout screech accelerating into an extreme high-RPM engine rev with a tense rising metallic scream, buildup to a launch" |
| `SFX_JW_Charge_Rush` | 11.0 m/s 발사 및 횡단 주행 | 바닥을 박차고 튀어나가는 폭발적 가속 + 지면을 찢는 바퀴 소리와 고속 도플러 디젤 포효 | `fast truck driveby whoosh`, `drag race launch`, `tearing dirt road heavy vehicle`, `roaring diesel exhaust` | "Massive armored tire vehicle launching with extreme speed, roaring diesel engine and tearing earth whoosh effect" |
| `SFX_JW_Charge_HitPlayer` | 돌진 중 플레이어 직접 충돌 | 쇳덩어리가 로봇 장갑을 들이받는 묵직한 둔기 충돌음 | `heavy metal crunch`, `car crash metal impact`, `heavy blunt hit robot`, `steel armor destruction slam` | "Devastating heavy metal impact as a giant rolling machine violently crashes into robot armor, crushing sound with low sub bass" |
| `SFX_JW_Charge_WallSlam` | 반대편 벽 충돌 (순간 압착) | 쾅! 콘크리트 벽에 거대 고철 바퀴가 격돌하는 거대한 충격음 + 파편 비산 | `heavy metal slam concrete`, `wrecking ball wall impact`, `metal debris collapse`, `hydraulic hammer hit` | "A massive metal wheel violently slams into a reinforced concrete wall, creating an explosive smash, crumbling debris and metallic shockwave" |
| `SFX_JW_Charge_Rebound` | 벽 충돌 직후 튕겨져 나옴 (1.4m 반동) | 거대한 고무 타이어가 강하게 튕겨나오는 탄성 퉁- 소리 | `heavy rubber bounce`, `elastic recoil thud`, `heavy spring bounce`, `pneumatic rebound` | "Heavy elastic rebound sound, large rubber tire bouncing back off a solid surface with a resonant low punch" |
| `SFX_JW_Charge_DecelRoll` | 반동 후 바퀴 감속 (0.4s Roll) | 튕겨 나온 바퀴가 지면을 굴러가며 서서히 제동되는 끼익- 스톱음 | `tire squeal stopping`, `rubber brake screech short`, `metal wheel rolling decelerate` | "Heavy rubber tire rolling to a stop on asphalt with a short screeching deceleration brake friction" |
| `SFX_JW_Charge_Interrupted` | 2.0s 전조 중 300 DPS 저지 성공 | 엔진 실화(Misfire) 푸슉- 털컥 김빠지며 시동이 꺼지고 스턴에 빠지는 소리 | `engine stall sputter`, `mechanical engine fail cough`, `hydraulic depressurize gasp`, `metal clunk shut down` | "A massive engine abruptly stalls and fails with a sputtering cough, metallic gear lock clunk, and losing air pressure" |
| `SFX_JW_Charge_FireTrail` | 헬 난이도: 배기 화염 장판 | 뿜어져 나온 화염이 바닥에서 2초간 활활 타오르는 화염 방사음 | `fire whoosh flame loop`, `ground fire burning roaring`, `gasoline fire burst`, `flamethrower hiss` | "A trail of blazing gasoline fire violently igniting along the ground, roaring crackling flames" |

---

### 2.3 [패턴 2] 3단 내려찍기 & 못판 유도 (Slam Attack x3)

| Cue ID | 상황 / 트리거 시점 | 사운드 묘사 및 레이어링 | 추천 검색 키워드 (Freesound / Sonniss) | AI SFX 생성 프롬프트 (ElevenLabs 등) |
| :--- | :--- | :--- | :--- | :--- |
| `SFX_JW_Slam_Launch` | 공중으로 수직 도약 (높이 3m) | 공압 실린더가 순간 방출되며 차체를 쏘아 올리는 슈욱- 파열음 | `pneumatic launch blast`, `hydraulic jump whoosh`, `heavy air cylinder release`, `spring piston fire` | "Powerful pneumatic piston release launching a heavy multi-ton machine straight into the air, forceful air burst whoosh" |
| `SFX_JW_Slam_Hover` | 체공 중 플레이어 추적 (1.0s) | 공중에서 거대한 바퀴가 공회전하며 바람을 가르는 묵직한 풍절음 | `heavy wind whoosh loop`, `rotating blade drone`, `air rush flutter heavy`, `fast spinning metal hum` | "A massive spinning wheel suspended in midair cutting through the wind with a heavy whipping whoosh and deep mechanical drone" |
| `SFX_JW_Slam_Drop` | 급강하 (0.22s) | 초고속으로 수직 낙하하는 날카로운 하강 풍절음 | `fast falling dive whoosh`, `meteor drop whistle`, `heavy downward whoosh`, `mortar incoming whistle` | "Fast plunging dive sound effect, heavy object descending rapidly from the air with sharp air friction" |
| `SFX_JW_Slam_Impact` | 일반 바닥 착지 충돌 | 쾅!! 크레이터를 짓이기며 지면을 강타하는 묵직한 대지 분쇄음 | `meteor ground impact`, `anvil drop thud`, `earthquake shock thud`, `heavy crater slam blast` | "Catastrophic ground slam as a heavy iron wheel crashes into the dirt floor, massive low sub bass shockwave and rock debris" |
| `SFX_JW_Nail_Puncture` | **[핵심 기믹] 고철 못판 위 착지** | 날카로운 쇠못 수십 개가 두꺼운 고무를 푹 뚫고 파고드는 쇠파열음 | `metal spike pierce rubber`, `nail puncturing thick tire`, `sharp metal crunch snap`, `tire puncture pop` | "Sharp iron spikes and nails viciously puncturing and tearing through a massive thick rubber tire, loud tearing snap" |
| `SFX_JW_Tire_Blowout` | 펑크 발생 직후 고압 공기 분출 | 쉭- 콰아아- 타이어 내부 초고압 공기가 폭발적으로 터져 나오는 소리 | `tire blowout blast`, `violent air hiss decompression`, `high pressure gas leak burst`, `steam pipe rupture` | "A violent high-pressure tire explosion and rapid depressurizing air hiss, violent burst of steam and pressurized gas" |

---

### 2.4 [패턴 3] 바이킹 순차 반원 탄막 (Jump Arc Barrage)

| Cue ID | 상황 / 트리거 시점 | 사운드 묘사 및 레이어링 | 추천 검색 키워드 (Freesound / Sonniss) | AI SFX 생성 프롬프트 (ElevenLabs 등) |
| :--- | :--- | :--- | :--- | :--- |
| `SFX_JW_Barrage_Jump` | 전장 중간 상단으로 도약 (1.7m) | 탄력 있게 솟구치는 중형 공압 점프음 | `hydraulic jump short`, `piston thrust mechanical`, `heavy jump whoosh` | "Medium pneumatic piston thrust launching an armored robot into an aerial arc" |
| `SFX_JW_Barrage_Shot` | 반원 각도 탄환 순차 투사 (7발) | 리듬감 있는 기계식 탄환 사출음 (0.35s 간격으로 틱! 틱! 틱!) | `rivet gun fire rapid`, `mechanical bolt shooter`, `nail gun metallic pop`, `pneumatic cannon fire` | "Punchy pneumatic rivet gun shot, metallic air-powered cannon firing a high-velocity steel bolt, crisp mechanical click" |
| `SFX_JW_Barrage_Fly` | 탄환 비행음 | 공기를 찢으며 날아가는 회전 탄환 풍절음 | `bullet whiz flyby`, `ricochet whistling`, `spinning metal projectile whoosh` | "A sharp spinning metal scrap projectile whizzing through the air past the listener" |
| `SFX_JW_Barrage_Hit` | 탄환 바닥/플레이어 착탄 | 소형 파편 폭발 또는 금속 방패 착탄음 | `small grenade pop metal`, `bullet hit steel plate`, `scrap metal burst`, `shrapnel ricochet` | "High-velocity metal slug crashing into metal armor with a sharp metallic clink and miniature explosive crack" |
| `SFX_JW_Barrage_Land` | 탄막 종료 후 지면 안착 | 제자리로 쿵 떨어지며 착지하는 고무 충격음 | `solid rubber thud ground`, `robot heavy footstep land`, `medium metallic ground thud` | "Heavy mechanical wheel landing firmly back on the floor with a solid thud and chassis creak" |

---

### 2.5 [패턴 4] 회전 톱날 당구 돌진 (Ricochet Blade Rush)

| Cue ID | 상황 / 트리거 시점 | 사운드 묘사 및 레이어링 | 추천 검색 키워드 (Freesound / Sonniss) | AI SFX 생성 프롬프트 (ElevenLabs 등) |
| :--- | :--- | :--- | :--- | :--- |
| `SFX_JW_Blade_Deploy` | 톱날 3날 전개 (Tell 시작) | 차체 안에서 날카로운 톱날이 찰칵- 튀어나오며 고정되는 기계 래치음 | `switchblade snap metal`, `mecha weapon deploy click`, `spring loaded blade snap`, `iron latch lock` | "Spring-loaded heavy steel circular blades violently snapping out of an armored vehicle, mechanical locking clank" |
| `SFX_JW_Blade_Spin_Loop` | 사선 돌진 중 톱날 초고속 회전 | 쒜에에엥- 귀를 찢는 공업용 앵글 그라인더/원형 톱날 초고속 회전 모터음 | `circular saw high speed spin`, `angle grinder cutting metal`, `buzzsaw whir loud`, `jet turbine scream` | "Industrial heavy buzzsaw spinning at hyper speed, high-frequency screaming metal friction whine cutting the air" |
| `SFX_JW_Blade_Bounce` | 당구 3쿠션 충돌 (벽/천장/바닥 3회) | 챙! 콰직! 벽면에 톱날이 격돌하며 튕겨 나가는 고주파 스파크 마찰음 | `sword clash sparks sharp`, `saw blade hitting metal bounce`, `ricochet metallic impact ping`, `heavy spark scrape` | "A massive high-speed spinning saw blade violently ricocheting off a metal wall, emitting explosive sparks and a sharp high-pitch metallic ping" |
| `SFX_JW_Blade_SlashHit` | 플레이어 접촉 타격 | 회전 톱날이 장갑을 갈아버리는 참격/분쇄 소리 | `metal saw cutting armor flesh`, `flesh tearing buzzsaw`, `chainsaw slash crunch`, `sparks grinding hit` | "A vicious circular saw blade grinding deep into robot armor plating, spraying sparks with a gnashing metal crunch" |
| `SFX_JW_Blade_Retract` | 착지 후 톱날 차체 수납 | 지잉- 찰칵! 톱날이 회전을 멈추고 내부로 쏙 들어가는 수납음 | `blade retract mechanism`, `mechanical latch slide lock`, `hydraulic cylinder retract`, `mecha holster sound` | "Rotating metal saw blade slowing to a halt and smoothly sliding back into its armored chassis with a solid mechanical lock" |

---

### 2.6 [패턴 5] 드리블 충격파 (Dribble Shockwave)

| Cue ID | 상황 / 트리거 시점 | 사운드 묘사 및 레이어링 | 추천 검색 키워드 (Freesound / Sonniss) | AI SFX 생성 프롬프트 (ElevenLabs 등) |
| :--- | :--- | :--- | :--- | :--- |
| `SFX_JW_Dribble_TellCrack` | 전조 바닥 균열 (0.6초) | 지면 암석이 찌이익 갈라지며 올라오는 불길한 저음 지진 진동 | `earth crack tear sound`, `rock split creak`, `sub earthquake rumble low`, `ground tearing deep` | "Deep tectonic rumble as the solid ground fractures and tears open, low frequency sub-bass vibration" |
| `SFX_JW_Dribble_Bounce_1` | 1차 도약 착지 (소형) | 통! 상대적으로 가벼운 고무 바운스음 + 약한 지면 떨림 | `basketball heavy rubber bounce`, `small boulder drop thud`, `rubber mallet hit dirt` | "Heavy rubber tire bouncing once on solid ground with a compact thud and subtle dirt crunch" |
| `SFX_JW_Dribble_Bounce_2` | 2차 도약 착지 (중형) | 텅! 무게가 실린 둔탁한 착지음 + 퍼져나가는 흙먼지 | `heavy rubber thump ground`, `car suspension bounce`, `dirt shockwave whoosh` | "Heavier mechanical bounce slam on ground, expanding dirt dust and resonant metallic thud" |
| `SFX_JW_Dribble_Bounce_3` | 3차 도약 착지 (대형) | 쿵!! 바닥을 뒤흔드는 충격음 + 암석 파편 튀는 소리 | `massive boulder drop slam`, `earth tremor impact`, `hydraulic press slam ground` | "Massive ground slam bounce with strong seismic shock, rock fragments shattering and heavy mechanical weight" |
| `SFX_JW_Dribble_Bounce_4` | 4차 도약 착지 (특대형 피날레) | **콰아아앙!!** 전장을 뒤흔드는 초대형 지진 폭발음 | `catastrophic ground slam`, `earthquake seismic rupture`, `explosive crater impact`, `deep bomb drop thud` | "Colossal seismic ground-shattering impact, massive explosive shockwave tearing through the entire battlefield with violent sub-bass rumble" |
| `SFX_JW_Dribble_Shockwave` | 충격파가 좌우로 질주 | 지면을 타고 파도처럼 밀려가는 흙/돌파편 러시 사운드 | `earthquake shockwave wave`, `debris avalanche rush`, `ground ripple rumble`, `rockslide whoosh` | "A fast-moving seismic wave rolling across the ground, churning stones, dust, and tearing earth shockwave" |
| `SFX_JW_Dribble_SteamExhaust`| 4회 종료 후 딜타임 (0.8s) | 치이익- 과열된 엔진과 피스톤에서 증기가 뿜어져 나오는 숨고르기 | `steam exhaust release loud`, `pneumatic air vent hiss`, `radiator steam hiss`, `pressure relief valve` | "Loud pressure-release valve hiss, high-pressure steam violently exhausting from an overheated engine" |

---

### 2.7 보스 상태 변화 및 피격/사망 (States & Transitions)

| Cue ID | 상황 / 트리거 시점 | 사운드 묘사 및 레이어링 | 추천 검색 키워드 (Freesound / Sonniss) | AI SFX 생성 프롬프트 (ElevenLabs 등) |
| :--- | :--- | :--- | :--- | :--- |
| `SFX_JW_Hit_Armor` | 일반 보스 피격 (코어/장갑) | 두꺼운 고철 장갑판을 타격하는 둔탁하고 단단한 쇳소리 | `heavy steel plate bullet hit`, `scrap metal hit dull`, `iron armor punch sound` | "Heavy blunt metallic hit against a thick scrap steel armor plate, resonant iron clank" |
| `SFX_JW_Hit_Rubber` | 타이어 부위 피격 | 두꺼운 생고무를 때리는 퍽- 소리와 찰진 탄성 마찰음 | `heavy rubber tire punch`, `dull thud rubber impact`, `boxing glove hit tire` | "A solid, dull, deep punch hitting a heavy thick rubber tire, damp low frequency thud" |
| `SFX_JW_Tire_Break_Final` | **타이어 영구 파괴 (펑크 2회)** | 콰지지직! 타이어가 완전히 찢겨나가고 **맨 쇠 휠 림(Rim)이 바닥을 긁는 끔찍한 금속 마찰음** | `bare wheel rim scraping concrete`, `catastrophic metal screech tire off`, `heavy train metal wheel grind` | "Violent destruction of a tire, rubber shredding apart and the bare iron wheel rim brutally screeching and grinding against concrete with intense friction sparks" |
| `SFX_JW_State_Groggy` | 4초 그로기 무력화 루프 | 시동이 꺼진 채 푸드득거리는 엔진 불발음과 고음의 전력 다운 사인파 | `dying engine sputter drone`, `power down sine whir`, `failing generator stutter`, `hydraulic shutdown` | "An engine struggling to breathe, sputtering irregular mechanical cough, declining pitch electronic power-down whine" |
| `SFX_JW_Phase2_Roar` | 2페이즈 진입 (체력 50% 이하 과열) | 화염 방출 이펙트 점화 + 배기 파이프 폭주 화염 포효음 | `jet engine afterburner ignite`, `engine overdrive roar`, `flamethrower ignition burst`, `fiery mechanical roar` | "A menacing mechanical roar as a diesel engine enters overdrive, afterburner fire bursting from exhaust pipes with a deep combustion rumble" |
| `SFX_JW_Defeat_Explosion` | 보스 토벌 및 코어 파괴 | 내부 부품이 차례대로 연쇄 폭발하며 거대한 고철 뼈대가 와르르 무너지는 소리 | `mecha massive explosion debris`, `scrap yard building collapse`, `series of mechanical explosions`, `heavy metal crash` | "A massive robot wheel boss catastrophically exploding from the inside, followed by secondary explosions and the thunderous collapse of heavy scrap metal debris" |

---

## 3. 필드 인터랙션 기믹 사운드 (Field Hazards)

| Cue ID | 오브젝트 | 사운드 묘사 | 검색 키워드 |
| :--- | :--- | :--- | :--- |
| `SFX_Field_NailBoard_Rise` | 고철 못판 (Nail Board) | 보스가 떨어질 때 못판 스프링이 찰칵 솟구치는 기계 작동음 | `bear trap spring snap`, `iron spikes deploy clack`, `mechanical trap trigger` |
| `SFX_Field_ExplosiveBox_Tick` | 폭발 화물 상자 (Explosive Box) | 보스 돌진에 닿기 직전 혹은 공격받을 때의 급박한 카운트다운 비프음 | `bomb countdown beep urgent`, `electronic fuse ticking fast`, `warning alarm short beep` |
| `SFX_Field_ExplosiveBox_Blast`| 폭발 상자 폭발 | 다이너마이트 묶음이 터지듯 시원하게 터지는 화약 폭발음 | `tnt dynamite explosion loud`, `industrial demolition blast`, `fuel barrel exploding burst` |

---

## 4. 보스전 BGM 명세: 헤비메탈 & 멜로딕 데스메탈 (Heavy / Industrial / Death Metal)

지역 1 보스 '호이루(정크 휠러)'의 **기름 냄새 나는 폐타이어, 중장비 디젤 엔진, 고속 회전 톱날**이라는 고철 투기장 정체성에 맞춰 **육중한 기타 리프와 강렬한 더블 베이스 드럼이 묵직하게 꽂히는 인스트루멘탈 메탈** 컨셉입니다.

### 4.1 두 가지 메탈 스타일 가이드 (기획 의도에 따른 선택)

| 스타일 옵션 | 추천 서브 장르 | BPM | 음악적 질감 & 호이루와의 시너지 |
| :--- | :--- | :--- | :--- |
| **옵션 A (중장비/육중함 중심)** | **Heavy Metal / Groove Metal / Industrial Metal** | **125 ~ 150 BPM** | 로우 튜닝 일렉 기타의 **묵직한 팜뮤트 리프(Chugging Riff)** + 타이어가 바닥을 짓이기듯 구르는 드럼 그루브. `Slam`·`Charge`의 둔탁한 무게감과 100% 일치 (DOOM / 매드맥스 / 람슈타인 느낌) |
| **옵션 B (공격성/폭주 중심)** | **Melodic Death Metal / Thrash Metal** | **165 ~ 195 BPM** | 고속 **트레몰로 피킹(Tremolo Picking)** + 타이트한 **더블 페달 킥(Double Bass)** + 날카로운 트윈 기타 멜로디. `Ricochet Blade`·2페이즈 과열의 파괴적 광기를 극대화 (보컬 제외 인스트루멘탈) |

---

### 4.2 BGM 검색 & AI 생성 키워드 매칭

| 구분 | Cue ID | 추천 검색 키워드 (스톡/무료 BGM 사이트) | AI 음악 생성기 프롬프트 (Suno AI / Udio / ElevenLabs Music) |
| :--- | :--- | :--- | :--- |
| **옵션 A: 헤비/그루브 메탈 (Phase 1 추천)** | `BGM_Boss1_JW_Heavy` | `heavy metal boss music instrumental`, `groove metal chugging riff`, `industrial metal fight theme`, `action metal guitar background`, `junkyard heavy rock metal` | **Style Tags**: `Heavy Metal, Groove Metal, Industrial Metal, 138 BPM, Low Tuned Guitar Chug, Heavy Palm Muted Riff, Punchy Drums, Aggressive, Boss Fight, Instrumental`<br>**Prompt**: *"An intense instrumental heavy groove metal boss battle theme at 138 BPM. Features crushing low-tuned electric guitar palm-muted riffs, driving double-kick drums, subtle metallic industrial clangs, and gritty distorted bass. Feels massive, mechanical, and relentless like a giant scrap-metal monster rolling in an arena."* |
| **옵션 B: 멜로딕 데스메탈 (고난도/Phase 2 추천)** | `BGM_Boss1_JW_Death` | `melodic death metal instrumental`, `thrash metal action game`, `fast metal double bass boss`, `dark shred guitar instrumental`, `extreme metal battle theme` | **Style Tags**: `Melodic Death Metal, Thrash Metal, 178 BPM, Fast Double Bass Drum, Shredding Twin Guitar Riff, Tremolo Picking, Dark & Fierce, Boss Theme, Instrumental`<br>**Prompt**: *"A ferocious instrumental melodic death metal boss track at 178 BPM. Blistering tight double-bass drumming, razor-sharp tremolo guitar riffs, menacing minor-key harmonies, and relentless high-octane energy. Raw, aggressive, and adrenaline-pumping without vocals."* |

> **AI 생성 시 네거티브(제외) 키워드 팁 (Exclude Styles)**:  
> 게임 전투 BGM에서 **보컬/그로울링(Growling)**이 들어가면 보스의 패턴 전조 사운드(`Tell`)나 피격음 가독성을 해칩니다. 반드시 **`vocals, singing, death growl, screaming, spoken word, rap, acoustic, soft ballad, lo-fi`**를 제외 태그로 지정해 순수 **연주곡(Instrumental)**으로 뽑는 것이 핵심입니다.

---

## 5. 유니티 오디오 믹서 및 구현 권장 설정 (Unity Audio Guidelines)

1. **피치 랜덤성 (Pitch Variation)**:
   - 반복적인 공격(연속 탄환 사출, 드리블 바운스, 보스 피격음)은 동일 클립 재생 시 청각적 피로감을 유발함.
   - 코드 또는 사운드 매니저에서 **Pitch를 `0.92f ~ 1.08f` 범위로 랜덤 조정**하여 재생.
2. **3D Spatial Sound (공간 음향 설정)**:
   - 횡스크롤 2D 뷰이므로 `Spatial Blend = 0.5 ~ 0.7` (완전 3D보다는 2.5D 센터 믹싱 권장).
   - `Min Distance = 3.0m`, `Max Distance = 25.0m`, `Rolloff Mode = Linear or Logarithmic`.
3. **덕킹 (Audio Ducking / Compression)**:
   - `SFX_JW_Charge_WallSlam`, `SFX_JW_Slam_Impact`, `SFX_JW_Dribble_Bounce_4` 같은 **결정타(Heavy Impact)** 발생 시:
   - 0.15초 동안 BGM과 일반 타격음을 약 `-4dB ~ -6dB` 순간 덕킹(Sidechain Compression) 처리하면 메탈 리프 사이로 타격감이 뚫고 나옵니다.
4. **BGM 볼륨 & EQ 밸런싱 (메탈 트랙 전용)**:
   - 메탈 장르는 기타 디스토션(`1kHz~4kHz`)과 더블 베이스 킥(`50Hz~100Hz`)의 음압(Loudness)이 매우 높으므로, **BGM 버스 볼륨을 SFX보다 약 `-6dB ~ -8dB` 낮게** 유지 (`0.40 ~ 0.55` 스케일).
   - 호이루의 **톱날 회전/금속 마찰음(`2kHz~5kHz`)**과 기타 배음이 겹쳐 답답해지지 않도록, Unity AudioMixer에서 BGM 그룹에 ParamEQ를 걸어 **`2.5kHz` 부근을 살짝(-2.5dB) 깎아주면** 효과음 가독성이 비약적으로 상승합니다.
5. **오디오 우선순위 (Priority)**:
   - BGM 및 보스 전조/궤적 사운드 (`BGM_Boss1_JW_*`, `SFX_JW_Charge_Tell`, `SFX_JW_Blade_Spin_Loop`): Priority `0 ~ 32` (절대 끊기지 않도록 최우선 순위).
   - 보스 일반 피격/타이어 피격 (`SFX_JW_Hit_Armor`): Priority `128` (인스턴스가 많을 시 음원 절약).
