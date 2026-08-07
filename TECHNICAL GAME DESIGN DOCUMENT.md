# TECHNICAL GAME DESIGN DOCUMENT

## Working Title
**Gloomveil**

## Genre
First-Person Psychological Horror / Maze Escape

## Platform
PC

## Team
- 2 Programmers
- 1 3D Artist

## Development Time
48 Hours

## Target Playtime
8–15 minutes per run

---

# 1. HIGH CONCEPT

Oyuncu karanlık, endüstriyel bir labirentten kaçmaya çalışır.

Labirentin içerisinde oyuncuyu avlayan tek bir yaratık bulunur.

Yaratığın temel davranışı oyuncuya doğrudan koşmak değildir.

Yaratık:

**Oyuncunun aktif görüş alanından kaçınarak yaklaşmaya çalışır.**

Oyuncu yaratığı fark edip doğrudan gözlerine bakarsa yaratık durur, agresif bir tepki verir ve geri çekilir.

Ancak her başarılı göz teması yaratığı kalıcı olarak daha tehlikeli hale getirir.

Oyuncu yaratığın çok yaklaşmasına izin verirse artık göz teması işe yaramaz.

Labirent içerisinde bulunan güvenli ışıklı bölgeler yaratığın giremeyeceği alanlardır. Ancak oyun ilerledikçe bu ışıklar kapanır.

Oyuncunun amacı belirli sayıda elektrik/sigorta panelini aktive edip çıkış kapısını açmak ve tesisten kaçmaktır.

---

# 2. CORE DESIGN PILLARS

## 2.1 The Monster Hunts Around Vision

Canavar oyuncuyu yalnızca takip etmemelidir.

Oyuncunun:

- baktığı yönü,
- hareket yönünü,
- yakın çevresini,
- safe zone'ları,
- karanlık alanları

hesaba katarak yaklaşmalıdır.

Ana his:

**“Canavar beni takip etmiyor. Beni avlıyor.”**

---

## 2.2 Information Has a Cost

Oyuncunun yaratığı kontrol etmesinin tek güvenilir yolu onu görmektir.

Ancak yaratığa her bakıldığında:

**Anger artar.**

Dolayısıyla:

- Bakmamak ölüm riski taşır.
- Bakmak gelecekteki karşılaşmaları zorlaştırır.

---

## 2.3 Darkness Belongs to the Monster

Oyuncu flashlight kullanabilir.

Ancak flashlight güvenlik sağlamaz.

Sadece environment ışıkları safe zone oluşturur.

Oyun ilerledikçe safe zone sayısı azalır.

Harita giderek yaratığın lehine dönüşür.

---

## 2.4 Player Must Keep Moving

Oyuncunun hedefi çıkışı yalnızca bulmak değildir.

Çıkışın aktif hale gelmesi için farklı noktalardaki fuse panellerini tamamlaması gerekir.

Bu sistem oyuncuyu sürekli:

- keşfetmeye,
- risk almaya,
- karanlığa girmeye

zorlar.

---

# 3. CORE GAMEPLAY LOOP

```text
Explore
↓
Find Fuse
↓
Check Surroundings
↓
Start Fuse Interaction
↓
Become Vulnerable
↓
Complete Fuse
↓
Lights Begin Shutting Down
↓
Monster Becomes More Aggressive
↓
Find Next Fuse
↓
Complete All Objectives
↓
Exit Opens
↓
Final Chase
↓
Escape
```

---

# 4. GAME STATES

Ana oyun state'leri:

## Initialization

Map, player, monster ve gameplay sistemleri hazırlanır.

## Exploration

Normal gameplay.

## FuseInteraction

Oyuncu fuse minigame ile meşguldür.

## FinalChase

Tüm fuse'lar tamamlanmıştır.

## Death

Oyuncu yakalanmıştır.

## Victory

Oyuncu çıkışa ulaşmıştır.

---

# 5. PLAYER SYSTEM

## Player Movement

Temel hareket:

- WASD
- Mouse Look
- Sprint
- Interaction
- Flashlight toggle

Jump gerekli değildir.

Crouch P2 özelliktir.

---

# 6. PLAYER MOVEMENT VALUES

Başlangıç değerleri:

Walk Speed:

**3.5–4 m/s**

Sprint:

**6–7 m/s**

Sprint Duration:

**5–6 saniye**

Full Stamina Recharge:

**7–9 saniye**

Exhaustion Delay:

**~1 saniye**

Bu değerler playtest sırasında değiştirilecektir.

---

# 7. STAMINA SYSTEM

Stamina yalnızca oyuncuyu yavaşlatmak için değil:

**kaçış kararlarını anlamlı hale getirmek**

için kullanılmalıdır.

Stamina:

- sprint sırasında azalır,
- sprint bırakıldığında belirli gecikmeden sonra dolar,
- tamamen bittiğinde kısa exhaustion oluşur.

Player'ın her zaman monster'dan kaçabilecek kadar sprint kullanabilmesi istenmez.

---

# 8. FLASHLIGHT

Oyuncuda oyun başından itibaren flashlight bulunur.

Flashlight:

- monster'ı durdurmaz,
- safe zone oluşturmaz,
- monster AI üzerinde doğrudan etki oluşturmaz.

Tek görevi:

**oyuncunun karanlıkta bilgi edinmesini sağlamak.**

Jam scope için battery kullanılmayacaktır.

---

# 9. MONSTER DESIGN

Canavar:

- uzun,
- zayıf,
- insansı,
- belirgin gözlere sahip,
- uzun kolları olan,
- karanlıkta silhouette'i kolay okunan

bir yaratık olmalıdır.

Gözleri gameplay açısından kritik olduğu için:

karanlıkta kolay fark edilmelidir.

Ancak bütün vücut net görünmemelidir.

---

# 10. MONSTER CORE RULES

Canavar:

- ışıklı safe zone'a giremez,
- flashlight'tan etkilenmez,
- sürekli ses çıkarmaz,
- oyuncunun aktif görüşünü değerlendirmeye çalışır,
- oyuncunun göremediği açılardan yaklaşmayı tercih eder.

Canavar normal bir chase enemy olarak tasarlanmamalıdır.

Ana amacı:

**visibility avoidance + intelligent positioning**

olmalıdır.

---

# 11. MONSTER AI OVERVIEW

Monster AI iki katmana ayrılır.

## Monster Brain

Canavarın yüksek seviyeli kararlarını verir.

Örneğin:

- stalk
- flank
- ambush
- retreat
- attack

## Navigation / Positioning

Brain tarafından verilen hedefe ulaşmak için:

- NavMesh
- pathfinding
- candidate points

kullanılır.

---

# 12. MONSTER STATE MACHINE

## Hidden

Canavar oyuncudan uzakta veya görüş dışında bulunur.

Bu state:

- spawn sonrası,
- retreat sonrası,
- reposition öncesi

kullanılabilir.

---

## Stalk

Ana state.

Canavar oyuncunun çevresinde fakat görüş alanının dışında kalmaya çalışır.

Hedef:

oyuncuya mümkün olduğunca yaklaşırken görünmemek.

---

## Reposition

Oyuncunun kamera yönü değiştiğinde mevcut yaklaşma açısı tehlikeli hale geldiyse canavar yeni bir pozisyon seçer.

Örneğin:

Oyuncu sağa baktı.

Canavar sağ koridordaysa:

canavar geri çekilip sol/arka taraftaki farklı bir route seçebilir.

---

## Flank

Canavar oyuncuya doğrudan yaklaşmak yerine alternatif bir rota seçer.

Hedef:

oyuncunun görüşünü dolaşarak daha avantajlı bir pozisyona geçmek.

---

## Ambush

Canavar hareket etmeyi bırakabilir.

Karanlık bir:

- köşe,
- junction,
- kapı yanı,
- corridor edge

gibi bölgede bekler.

Oyuncunun kendisine yaklaşmasını bekler.

---

## CutOff

Oyuncunun mevcut hareket yönünden ileride bulunan bir node'a gitmeye çalışır.

Canavar:

**player position**

yerine:

**predicted player position**

hedefler.

---

## Observed

Oyuncu doğrudan yaratığın gözlerine baktığında tetiklenir.

Canavar:

- anında durur,
- aggressive scream/reaction oynatır,
- Anger +1 alır.

Ardından Retreat state'e geçer.

---

## Retreat

Canavar:

- oyuncudan uzaklaşır,
- mümkünse görüş alanından çıkar,
- mümkünse karanlık bölgeye gider.

Retreat tamamen despawn anlamına gelmez.

---

## Fake Retreat

Canavar bazen tamamen uzaklaşmaz.

Görüşten çıktıktan sonra yakın bir:

- köşe,
- yan corridor,
- kapı arkası

gibi yerde kalabilir.

---

## Commit

Canavar oyuncuya Critical Distance'tan daha yakına gelirse:

gaze artık monster'ı durduramaz.

Canavar doğrudan attack state'e geçer.

---

## Kill

Monster attack tamamlar.

Player control kapatılır.

Kısa death sequence oynar.

---

# 13. PLAYER VISION MODEL

Canavar AI'ın en önemli girdilerinden biri:

**Player Vision Cone**

olacaktır.

Player Camera üzerinden:

- camera forward
- horizontal FOV
- vertical FOV

bilgileri alınır.

World point'in oyuncu tarafından görülüp görülmediği yaklaşık olarak hesaplanabilir.

İki seviye visibility kullanılabilir.

## Potentially Visible

Point FOV içerisindedir.

## Actually Visible

FOV içinde + player ile point arasında obstacle yoktur.

---

# 14. MONSTER POSITION CANDIDATES

Canavar her frame pozisyon hesaplamamalıdır.

Belirli aralıklarla:

**0.4–0.8 saniye**

çevrede candidate stalk points değerlendirilir.

Candidate point kaynakları:

- predefined level nodes,
- NavMesh sampled points,
- corridor anchors,
- junction anchors,
- room entrance points.

Jam için en stabil yöntem:

**Level designer tarafından bırakılmış StalkPoint component'leri.**

---

# 15. STALK POINT SYSTEM

Map üzerinde invisible StalkPoint objeleri bulunur.

Her StalkPoint metadata taşıyabilir:

- DarknessLevel
- IsNearCorner
- IsJunction
- IsDeadEnd
- IsSafeZoneAdjacent
- AmbushAllowed

Canavar gerektiğinde bu noktaları değerlendirir.

---

# 16. STALK POINT SCORING

Monster candidate noktaları score ile değerlendirir.

Örnek kriterler:

## Positive

Player FOV dışında:

+High

Karanlık:

+High

Player'a yakın:

+Medium

Player'ın arkasında:

+Medium

Predicted route üzerinde:

+Medium

Corner/junction:

+Medium

Safe zone exit yakınında:

+Low/Medium

---

## Negative

Player tarafından görülebilir:

-Huge

Safe zone içinde:

-Invalid

NavMesh path yok:

-Invalid

Player'a aşırı uzak:

-Medium

Monster'ın mevcut pozisyonundan gereksiz uzak:

-Low

---

# 17. VISIBILITY AVOIDANCE

Monster'ın temel davranışı:

**görünmemeye çalışmak.**

Ama bu sistem mutlak olmamalıdır.

Canavar:

- mümkünse görünmez,
- uygun route yoksa kısa süre FOV içinden geçebilir,
- yüksek Anger seviyesinde daha riskli hareketler yapabilir.

Bu sayede AI robotik davranmaz.

---

# 18. PLAYER DIRECTION PREDICTION

Monster oyuncunun hareket yönünü analiz eder.

Input veya velocity üzerinden:

**PredictedPlayerPosition**

yaklaşık olarak hesaplanır.

Örneğin:

CurrentPosition + Velocity × PredictionTime

PredictionTime:

yaklaşık 1–2 saniye.

CutOff behavior bu noktaya göre çalışır.

---

# 19. ANGER SYSTEM

Oyuncunun monster'ın gözlerine her başarılı bakışı:

**Anger +1**

oluşturur.

Anger yalnızca movement speed artırmamalıdır.

AI davranışını değiştirmelidir.

---

# 20. ANGER STAGES

## Anger 0–2

Monster sabırlıdır.

- daha uzaktan stalk,
- düşük flank oranı,
- daha uzun reposition cooldown.

---

## Anger 3–5

Monster daha agresiftir.

- daha yakın stalk,
- daha fazla flank,
- daha fazla ambush.

---

## Anger 6–8

Monster oyuncunun davranışını kesmeye çalışır.

- cut-off artar,
- safe zone çevresinde bekleyebilir,
- daha kısa reposition süresi.

---

## Anger 9+

Monster maksimum agresif.

- yakın approach,
- daha riskli FOV geçişleri,
- daha hızlı movement,
- daha fazla fake retreat,
- aggressive cut-off.

---

# 21. SPEED SCALING

Her successful gaze:

yaklaşık:

**+%5 movement speed**

Kazandırır.

Matematik:

CurrentSpeed = BaseSpeed × AngerMultiplier

Maximum speed için cap olmalıdır.

Öneri:

**Max = Base × 1.7–1.8**

---

# 22. GAZE SYSTEM

Oyuncunun yalnızca monster'a doğru dönmesi yeterli değildir.

Oyuncu:

**monster'ın gözlerine bakmalıdır.**

Monster head üzerinde:

GazeTarget

bulunur.

---

# 23. GAZE DETECTION

Camera center çevresinden birkaç raycast kullanılabilir.

Öneri:

5 raycast.

Örneğin:

```text
•       •

    •

•       •
```

En az belirli sayıda ray:

GazeTarget collider'a ulaşıyorsa:

gaze candidate oluşur.

---

# 24. GAZE CONFIRMATION

Gaze anlık olmamalıdır.

Oyuncu gözlere yaklaşık:

**0.15–0.25 saniye**

bakmalıdır.

Arada obstacle varsa gaze geçersizdir.

---

# 25. CRITICAL DISTANCE

Monster oyuncuya belirli mesafeden daha fazla yaklaştığında:

gaze sistemi etkisiz olur.

Başlangıç değeri:

**2–3 metre**

Bu noktada:

Monster Commit state'e geçer.

Oyuncunun dönmesi artık kurtarmaz.

---

# 26. MONSTER AUDIO

Monster'ın en önemli korku unsurlarından biri:

**sessizliği.**

Normal stalking sırasında:

- footstep minimum veya yok,
- growl yok,
- sürekli monster ambience yok.

Oyuncu canavarın tam yerini audio üzerinden öğrenmemelidir.

---

# 27. SUBTLE WARNING SYSTEM

Monster yakınsa çok hafif feedback olabilir.

Örneğin:

- heartbeat,
- düşük frequency rumble,
- hafif screen tension,
- flashlight flicker.

Bu feedback:

monster'ın yönünü söylememelidir.

---

# 28. MONSTER OBSERVED REACTION

Oyuncu monster'a baktığında:

- movement freeze,
- baş/vücut recoil,
- yüksek scream,
- defensive posture,
- retreat.

Bu an oyunun en güçlü görsel/audio feedback anlarından biridir.

---

# 29. SAFE ZONE SYSTEM

Environment içerisinde belirli ışıklı alanlar:

SafeZone

olarak işaretlenir.

Monster:

SafeZone içerisinde destination seçemez.

SafeZone içerisindeki player saldırıya uğrayamaz.

---

# 30. SAFE ZONE EDGE BEHAVIOR

Monster safe zone'un içine giremez.

Ancak:

- dışındaki karanlıkta bekleyebilir,
- başka çıkışa reposition olabilir,
- player'ın çıkış yönünü tahmin edebilir.

Safe zone monster AI'ı tamamen devre dışı bırakmamalıdır.

---

# 31. LIGHT ZONE SYSTEM

Map içerisindeki environment ışıkları zone olarak gruplanır.

Örneğin:

- Zone A
- Zone B
- Zone C
- Zone D

Her LightZone:

- active
- flickering
- disabled

durumlarına sahip olabilir.

---

# 32. LIGHT DIRECTOR

LightDirector oyun progression'ına göre zone'ları kapatır.

Örneğin:

## Start

%100 veya %80 light aktif.

## Fuse 1

Bir zone söner.

## Fuse 2

Ek zone söner.

## Fuse 3

Haritanın büyük bölümü karanlık olur.

## Final Chase

Yalnızca birkaç kritik ışık kalır.

---

# 33. FUSE SYSTEM

Oyuncunun exit'i açması için:

**3 fuse**

tamamlaması önerilir.

Her FuseBox:

- interactable,
- incomplete,
- active

state'lerine sahiptir.

---

# 34. FUSE MINIGAME

Minigame mekanik olarak basit olmalıdır.

Amaç:

**oyuncunun kamerasını ve dikkatini environment'tan almak.**

Örnek:

3 cable'ı doğru portlara bağlamak.

Ortalama süre:

**5–8 saniye**

---

# 35. FUSE VULNERABILITY

Minigame sırasında:

- player hareket edemez veya çok kısıtlıdır,
- normal camera control kapalı olabilir.

Player istediği zaman minigame'i iptal edebilir.

Jam için:

Cancel → progress reset

önerilir.

---

# 36. MONSTER FUSE BEHAVIOR

Monster fuse interaction'ı algılayabilir.

Ancak doğrudan player'a koşmak yerine:

yüksek score'lu approach position arar.

Bu özellikle güçlü bir AI hissi oluşturur.

Player minigame'i tamamlayıp döndüğünde:

monster çoktan uygun pozisyonda olabilir.

---

# 37. EXIT SYSTEM

Tüm fuse'lar aktive olduğunda:

- Exit Powered event tetiklenir.
- Exit açılır.
- Environment feedback oluşur.
- FinalChase state başlar.

---

# 38. FINAL CHASE

Final chase sırasında:

- LightZone'ların büyük bölümü kapanır.
- Monster aggression artar.
- Reposition cooldown azalır.
- Monster daha yakın candidate point'leri seçer.

Monster'ın current Anger değeri korunur.

Dolayısıyla:

oyuncu run boyunca monster'a ne kadar çok baktıysa final o kadar zordur.

---

# 39. LEVEL DESIGN

İlk oynanabilir sürüm:

**seeded procedural modular maze**

kullanacaktır.

- Kat sayısı Inspector'dan ayarlanır; desteklenen aralık `2+` kattır.
- Sistem yerleşim hesabında gizli 4 × 4 m lattice kullanır; oyuncuya görünen odalar ve koridorlar değişken boyutlu modüllerdir.
- `1×2`, `2×2` ve `2×3/3×2` oda footprint'leri zorunludur.
- Düz koridorlar 4, 8 ve 12 m varyasyonlarını; tüm modüller 0°, 90°, 180° ve 270° yönlerini destekler.
- Her komşu kat çifti iki ayrı merdivenle bağlanır.

---

# 40. MAP REQUIREMENTS

Map'te bulunmalı:

- 4, 8 ve 12 m düz corridor run'ları
- değişken boyutlu ve farklı yönlere döndürülmüş odalar
- T junction
- cross junction
- blind corners
- loops
- dead ends
- small ve large technical rooms
- unrelated odalar arasında boş negative space
- her komşu kat çifti için iki separated stair
- safe zones
- fuse rooms
- exit room

---

# 41. AI-FRIENDLY LEVEL DESIGN

Map yalnızca oyuncu için değil:

**monster AI için de tasarlanmalıdır.**

Her bölgede birden fazla approach route olması tercih edilir.

Uzun tek yönlü corridor'lar sınırlı kullanılmalıdır.

---

# 42. STALK POINT PLACEMENT

Üretilen modüllerde neutral StalkPoint anchor'ları özellikle:

- blind corners,
- behind pillars,
- junction edges,
- side rooms,
- safe zone exits,
- corridor turns

üzerinde konumlandırılır.

Map sistemi bu noktaları AI davranışına yorumlamaz; doğrulanmış metadata olarak yayınlar.

---

# 43. SEEDED RUN RANDOMIZATION

Her run'ın map topology'si ve gameplay yerleşimleri aynı seed üzerinden deterministik olarak üretilebilir.

Random:

- floor'lara dağıtılmış fuse positions,
- initial monster position,
- light shutdown order,
- bazı locked/open doors,
- stalk behavior seed.

Aynı seed aynı topology signature'ı üretir; gameplay ve presentation random stream'leri birbirinden ayrılır.

---

# 44. PROCEDURAL MAP GENERATION — P0

Generator aşağıdaki sırayla çalışır:

- seed ve Inspector config doğrulama,
- variable-footprint oda ve merdiven yerleşimi,
- corridor routing ve görünür modül planlama,
- start, exit, fuse ve safe-zone rol atama,
- prefab çözümleme ve görünür map build,
- NavMesh build ve runtime path validation,
- gameplay/AI kullanımına tek seferlik publication.

Başarısız veya navigation doğrulamasından geçemeyen map yayınlanmaz. Map generation katmanı enemy code çağırmaz.

---

# 45. PROCEDURAL MAZE VISUALIZER

Gizli placement lattice yalnızca yerleşim ve routing hesabı içindir; kullanılmayan lattice hücreleri için mesh veya filler prefab oluşturulmaz.

Odalar `1×2`, `2×2`, `2×3/3×2` görünür footprint'lerle yerleştirilir. Corridor neighbour mask ve straight-run uzunluğu üzerinden module seçilir.

Örneğin:

N + S:

Straight

E + W:

Straight rotated

N + E:

Corner

N + E + W:

T Junction

N + S + E + W:

Cross

Single connection:

Dead End

Degree-two straight run'lar 4, 8 ve 12 m görünür modüllere sıkıştırılır. Corner, junction, room door veya stair landing üzerinden tek bir düz modül geçirilmez.

---

# 46. PROCEDURAL EXIT ASSIGNMENT

Exit kuş uçuşu distance ile seçilmemelidir.

Graph üzerinden:

**Start'tan weighted shortest-path distance açısından en uzak uygun room**

seçilmelidir.

Graph ağırlıkları yatay traversal mesafesini ve katlar arası stair traversal maliyetini birlikte hesaba katar.

---

# 47. PROCEDURAL OBJECTIVE PLACEMENT

Fuse'lar:

- `min(fuseCount, floorCount)` kadar floor coverage sağlayan,
- farklı branches,
- uzak dead-end'ler,
- birbirinden ayrılmış bölgeler

arasından seçilebilir.

Bu sistem player'ı gerçekten map'i keşfetmeye zorlar.

---

# 48. PROCEDURAL MODULE METADATA

Her module gameplay/AI'dan bağımsız neutral metadata taşıyabilir:

- `StalkPoint_*`
- `Anchor_SafeZone`
- `LightAnchor`
- `Anchor_Fuse_00`
- MonsterSpawnCandidate
- `Landing_Bottom` / `Landing_Top`

Generator, Start'tan Exit'e, tüm Fuse anchor'larına ve bütün stair landing'lerine complete NavMesh path bulunduğunu doğruladıktan sonra map'i registry üzerinden gameplay/AI kullanımına yayınlar.

---

# 49. SYSTEM ARCHITECTURE

Önerilen sistem ayrımı:

## GameManager

- game states
- restart
- victory
- death

## PlayerController

- movement
- camera

## StaminaController

- drain
- regen

## PlayerInteraction

- interaction ray

## FlashlightController

- flashlight state

---

# 50. MONSTER ARCHITECTURE

## MonsterBrain

High-level AI state machine.

## MonsterNavigator

NavMesh movement.

## MonsterPerception

Player:

- position,
- view direction,
- visibility

bilgisi.

## MonsterPositionEvaluator

Candidate point scoring.

## MonsterAngerController

Anger + speed + aggression.

## MonsterGazeReceiver

Gaze detection target.

## MonsterAnimator

Animation state kontrolü.

---

# 51. WORLD SYSTEMS

## StalkPoint

AI navigation candidate.

## SafeZone

Monster restriction.

## LightZone

Group light state.

## LightDirector

Progressive shutdown.

## FuseBox

Objective.

## ObjectiveManager

Fuse count.

## ExitController

Final objective.

---

# 52. DATA CONFIGURATION

Değerler mümkün olduğunca Inspector / ScriptableObject üzerinden tune edilmelidir.

Örneğin:

MonsterConfig:

- base speed
- max speed
- critical distance
- gaze duration
- reposition interval
- FOV avoidance weight
- darkness weight
- player distance weight
- prediction time
- anger multipliers

Bu sayede jam sırasında hızlı balancing yapılabilir.

---

# 53. DEBUG TOOLS

AI debug visualization çok önemlidir.

Editor/Gizmo'da gösterilebilmeli:

- Player FOV
- Monster current state
- Current target point
- Candidate StalkPoints
- Candidate scores
- Predicted player position
- Safe zones
- Critical distance
- Current Anger

AI debug olmadan behavior tuning ciddi zaman kaybettirebilir.

---

# 54. AI TEST SCENES

Ana map'ten bağımsız küçük test scene yapılması önerilir.

## Test 1

Straight corridor.

Amaç:

gaze + critical distance.

## Test 2

T Junction.

Amaç:

flank.

## Test 3

Loop.

Amaç:

cut-off.

## Test 4

Safe zone.

Amaç:

safe zone edge behavior.

---

# 55. MONSTER AI PRIORITY

AI özellikleri priority sırası:

## P0

- NavMesh navigation
- FOV detection
- stalk outside vision
- gaze
- retreat
- attack
- anger

## P1

- reposition
- flank
- ambush
- player prediction

## P1+

- cut-off
- fake retreat
- safe zone camping

## P2

- complex adaptive behavior
- advanced memory
- procedural-aware tactical planning

---

# 56. ART REQUIREMENTS

## Monster

1 model.

Target height:

yaklaşık 2.4–2.7 metre.

Silhouette:

- long arms
- long fingers
- hunched posture
- distinctive head
- clearly readable eyes

---

# 57. MONSTER ANIMATIONS

Minimum:

- Idle
- Stalk Walk
- Fast Walk / Run
- Observed Reaction
- Retreat
- Attack

Minimum viable:

- Walk
- Observed
- Attack

---

# 58. ENVIRONMENT KIT

Minimum:

- Straight Corridor
- Corner
- T Junction
- Cross Junction
- Dead End
- Small Room
- Fuse Room
- Exit Room

---

# 59. ENVIRONMENT PROPS

P0:

- ceiling light
- fuse box
- exit door
- pipes
- electrical cabinet
- crate
- cable
- wall panel

P1:

- shelves
- broken terminal
- barrels
- debris
- vents

---

# 60. AUDIO REQUIREMENTS

## Environment

- industrial ambience
- electric hum
- light buzz
- distant metal sounds

## Player

- walk
- sprint
- breathing
- exhaustion

## Monster

Normal movement:

almost silent.

Required:

- gaze scream
- attack
- kill

## Gameplay

- fuse interaction
- fuse complete
- lights off
- exit powered
- exit open

---

# 61. 48-HOUR DEVELOPMENT PRIORITY

## First 6–8 Hours

Goal:

Core monster prototype.

Required:

- player movement
- NavMesh monster
- player FOV
- monster avoids visibility
- gaze freezes monster
- kill

Bu noktada oyun eğlenceli değilse diğer sistemlere geçilmemelidir.

---

# 62. HOURS 8–16

- stamina
- anger
- retreat
- critical distance
- stalk point scoring
- graybox map
- safe zones

---

# 63. HOURS 16–24

- fuse system
- fuse minigame
- light zones
- exit
- full game loop

24. saat civarında oyun baştan sona bitirilebilir olmalıdır.

---

# 64. HOURS 24–34

AI polish:

- flank
- reposition
- ambush
- cut-off

Art:

- monster
- environment
- animation

Audio integration.

---

# 65. HOURS 34–42

- balancing
- final chase
- random fuse
- random light order
- procedural acceptance seed checks
- NavMesh ve artist replacement batch doğrulaması
- bug fixing

Procedural map bu aralıkta başlanacak yeni bir feature değildir; ilk oynanabilirin P0 parçasıdır. Bu aşamada yalnızca kabul, tuning ve polish yapılır.

---

# 66. HOURS 42–48

Yeni feature eklenmez.

Sadece:

- bugs
- polish
- lighting
- sound
- performance
- build
- submission

---

# 67. ABSOLUTE MVP

Minimum bitmiş oyun:

- seeded procedural modular maze, Inspector-configurable `2+` floors
- player movement
- sprint/stamina
- flashlight
- one monster
- FOV avoidance
- gaze
- anger speed increase
- critical distance
- safe zones
- 3 fuse
- lights shutting down
- exit
- final chase
- death
- win

---

# 68. MAIN TECHNICAL GOAL

Procedural map zorunlu P0 altyapıdır; ancak tek başına oyunun ana teknik başarı ölçütü değildir.

Oyunun teknik başarısı:

**Monster AI'ın oyuncunun görüş alanını gerçekten dikkate alıyormuş gibi hissettirmesidir.**

İdeal oyuncu deneyimi:

> Oyuncu koridorda ilerler.
>
> Canavarı göremez.
>
> Oyuncu sağ tarafa bakınca yaratık mevcut yolunu değiştirir.
>
> Oyuncu tekrar ilerler.
>
> Bir junction'a geldiğinde yaratığı yan koridorda fark eder.
>
> Göz göze gelirler.
>
> Yaratık scream atıp geri çekilir.
>
> Oyuncu ilerlemeye devam eder.
>
> Birkaç dakika sonra yaratık artık daha hızlıdır ve player'ın önünü kesmeye başlar.

Oyuncunun oyun bittikten sonra söylemesi istenen şey:

**“Bu şey gerçekten nerede olduğuma değil, nereye baktığıma göre hareket ediyordu.”**

Bu his sağlanırsa oyunun ana fikri başarıyla çalışmış demektir.
