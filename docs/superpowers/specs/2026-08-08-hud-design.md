# HUD Tasarımı — Gloomveil

Tarih: 2026-08-08
Durum: Onaylandı, uygulanmayı bekliyor

## 1. Amaç ve kapsam

Oyunda hiç HUD yok. Bu tasarım, oyuncunun karar vermek için ihtiyaç duyduğu dört bilgiyi ekrana
getirir ve bunu kameranın hareketine eylemsizlikle tepki veren bir sunumla yapar.

Kapsam içi:

- Crosshair (nişangah)
- Stamina barı
- Sigorta hedef sayacı
- Fener durumu
- Etkileşim prompt'u
- Kamera hareketine bağlı HUD eylemsizliği (sway)
- Oyun durumuna göre HUD görünürlüğü

Kapsam dışı (bilinçli olarak):

- Fener pili — TGD'de pil mekaniği yok. Bu HUD işinin kapsamı dışında, ama tasarım kararı olarak
  reddedilmiş de değil: zaman kalırsa sonradan eklenebilir. Eklendiğinde HUD tarafında maliyeti
  `FlashlightWidget`'a bir doluluk barı eklemek ya da yanına ikinci bir widget koymaktır; hiyerarşi,
  hareket modeli veya assembly düzeni değişmez (bkz. 3.4 — widget başına bileşen yaklaşımının
  satın aldığı şey tam olarak budur)
- Can barı / hasar göstergesi — TGD'de HP yok, saldırı tek vuruşta öldürür
- Monster yakınlık göstergesi (kalp atışı, vinyet) — TGD bölüm 11'in "doğrudan kovalamaz, görüş
  dışında stalk eder" tasarımını doğrudan zayıflatır; ayrı bir tasarım tartışması gerektirir
- Safe zone göstergesi
- Menü, pause, ayarlar ekranları
- `FuseMiniGameController`'ın taşınması veya yeniden yazılması — o UI olduğu yerde kalır

### Kaynak doküman durumu

TGD'de HUD/UI diye bir bölüm **yok**. Bu tasarımın oyun kuralı dayanağı olmadığı anlamına gelmez;
gösterilen her veri TGD'de tanımlı bir sistemden okunur (bölüm 6-7 hareket/stamina, bölüm 33-36
sigorta, bölüm 8 fener, bölüm 49 etkileşim). Sunum kararları bu dokümanın kendisine aittir.

## 2. Onaylanan kararlar

| Karar | Seçim | Gerekçe |
|---|---|---|
| HUD felsefesi | Klasik / sabit (B) | Jam süresinde oyuncunun hedefi anlaması, gerilim inceliğinden önce gelir |
| Yerleşim | Dört köşe | Elemanlar birbirine karışmaz, her bilginin tek ve tanıdık bir yeri olur |
| Crosshair sway'e dahil mi | **Hayır** | Nişan noktası daima ekran merkezi olmak zorunda (bkz. 3.1) |
| Mimari | Widget başına küçük bileşen + paylaşılan sway | Codebase'in mevcut deseni; her parça tek başına anlaşılır |
| Assembly | Yeni `Game.UI` | HUD saf okuyucu; tek yönlü bağımlılık bedavaya geliyor |
| Veri akışı | Poll (widget kaynağını okur) | Event tabanlı model ileride gerekebilir, şimdi değil (bkz. 8) |
| Sway varsayılan yönü | Hepsi aynı yöne, farklı miktarda | Fiziksel olarak dürüst olan bu; yelpaze isteğe bağlı ayar |

### 2.1 Crosshair neden sabit

`PlayerGazeDetector` ekran merkezinden 5 noktalı paralel ışın kümesi atar
(`Assets/_Project/Code/Gameplay/Player/PlayerGazeDetector.cs`), `PlayerInteraction` da tam
merkezden `camera.transform.forward` yönünde ışın atar
(`Assets/_Project/Code/Gameplay/Interaction/PlayerInteraction.cs`). İkisinin de nişan noktası
**değişmez biçimde ekranın tam ortasıdır**.

Crosshair kayarsa oyuncuya yalan söyler. TGD bölüm 19-21'e göre monster'a göz teması kurmak
çekirdek mekanik ve Anger'ı **kalıcı** olarak yükseltir; oyuncunun "baktım ama tetiklenmedi" ya da
"bakmadım ama tetiklendi" yaşaması, sebebini göremeyeceği bir hata olur.

Bu kural hiyerarşiyle zorlanır, disiplinle değil: crosshair `FixedRoot` altında yaşar, `SwayRoot`
altında değil. Sway bileşenleri `SwayRoot`'a bağlıdır, dolayısıyla crosshair'e ulaşamaz.

## 3. Mimari

### 3.1 Assembly

Yeni: `Assets/_Project/Code/UI/Game.UI.asmdef`

```
name: Game.UI
rootNamespace: Game.UI
references: Game.Core, Game.Gameplay, UnityEngine.UI
```

`Game.Gameplay` `Game.UI`'ı **referans almaz**. Bağımlılık tek yönlüdür: UI gameplay durumunu okur,
gameplay UI'ın varlığından habersizdir. `SceneWiring` HUD'ı bağlamaz; widget'lar kendi
referanslarını Inspector'dan alır. Bu mümkün, çünkü tüm kaynaklar (`PlayerController`,
`ObjectiveManager`, `PlayerInteraction`, `FlashlightController`, `GameFlowController`) sahne
yüklendiğinde zaten mevcuttur — hiçbiri runtime'da üretilmez.

### 3.2 UI teknolojisi

Legacy `UnityEngine.UI` (`Text`, `Image`, `CanvasGroup`) kullanılır — `FuseMiniGameController`'ın
zaten kullandığı sistem. TextMeshPro projede referanslı değil; jam ortasında yeni bağımlılık
açmanın karşılığı yok.

Harici sprite gerekmez: barlar ve çerçeveler düz `Image`, ikonlar Unity'nin dahili UI sprite'ları.

### 3.3 Sahne hiyerarşisi

```
HudCanvas                       Screen Space - Overlay
│                               CanvasScaler: Scale With Screen Size, 1920x1080, Match 0.5
│                               CanvasGroup  (HudVisibility bunu sürer)
├── SwayRoot                    HudRootSway
│   ├── ObjectiveWidget         sol üst    + HudSway
│   ├── StaminaWidget           sol alt    + HudSway
│   ├── FlashlightWidget        sağ alt    + HudSway
│   └── InteractionPromptWidget orta alt   + HudSway (küçük çarpan)
└── FixedRoot                   sway yok, asla
    └── CrosshairWidget         + CrosshairPulse
```

Ekran düzeni:

```
+--------------------------------------------+
|  SIGORTA  1/3                              |
|                                            |
|                                            |
|                                            |
|                     +                      |
|                                            |
|              [E] Sigortayi calistir        |
|                                            |
|                                            |
|  [||||||--]                            (F) |
+--------------------------------------------+
```

### 3.4 Dosyalar

| Dosya | Sorumluluk |
|---|---|
| `UI/Game.UI.asmdef` | Assembly tanımı |
| `UI/Hud/HudCameraMotion.cs` | Kameranın açısal hızını ölçen tek kaynak |
| `UI/Hud/HudSpring.cs` | Saf yay-sönüm entegratörü (static, MonoBehaviour değil) |
| `UI/Hud/HudRootSway.cs` | Kök öteleme + z rotasyonu ("cam levha") |
| `UI/Hud/HudSway.cs` | Widget başına öteleme (parallax) |
| `UI/Hud/HudVisibility.cs` | Oyun durumuna göre CanvasGroup alpha |
| `UI/Hud/CrosshairWidget.cs` | Nişangah görünümü + hedef vurgusu |
| `UI/Hud/CrosshairPulse.cs` | Nişangah ölçek/opaklık tepkisi (konum yok) |
| `UI/Hud/StaminaWidget.cs` | Stamina barı |
| `UI/Hud/ObjectiveWidget.cs` | Sigorta sayacı |
| `UI/Hud/InteractionPromptWidget.cs` | Etkileşim metni |
| `UI/Hud/FlashlightWidget.cs` | Fener ikonu |

## 4. Hareket modeli

### 4.1 `HudCameraMotion`

Tüm sway bileşenlerinin okuduğu tek ölçüm kaynağı.

`LateUpdate`'te çalışır. Gerekçe: `PlayerController.HandleLook()` kamerayı `Update`'te döndürür;
`LateUpdate` o karenin nihai rotasyonunu görür, aksi halde HUD kalıcı olarak bir kare geriden gelir.

Ölçümü, Inspector'dan atanan kamera `Transform`'unun rotasyonundan yapar — `PlayerController`'dan
**değil**. Böylece kamerayı ne sürerse sürsün (oyuncu girdisi, ileride bir cutscene, editör
oynatması) doğru çalışır ve `Game.UI`'ın oyuncu tipine bağımlılığı hareket katmanına sızmaz.
Ayrıca `PlayerController` pitch'i dışarı açmıyor; Transform okumak bunu gerektirmez.

Algoritma:

1. `Mathf.DeltaAngle` ile yaw ve pitch farkını al, `Time.deltaTime`'a böl → derece/saniye
2. Üstel yumuşatma uygula: `smoothed = Lerp(smoothed, raw, 1 - Exp(-smoothing * dt))`
3. `maxTrackedRate` ile kırp

Işınlanma koruması: tek karede `teleportThresholdDegrees` (varsayılan 1500 °/s) eşiğini aşan fark
bir bakış hareketi değil, bir konum sıçramasıdır — o kare atılır ve hız sıfırlanır.
`PlayerController.Teleport` (spawn, ileride level geçişi) HUD'ı sarsmaz.

Açık API: `Vector2 AngularVelocity` (x = yaw °/s, y = pitch °/s), `void ResetMotion()`.
`ResetMotion()` ışınlanma korumasının kendi içinde çağırdığı metottur; ayrıca public'tir, böylece
ileride bir sahne/level geçişi HUD'ı açıkça sıfır hızda başlatabilir.

Kamera referansı null ise `AngularVelocity` sıfır kalır — HUD sallanmaz ama çalışmaya devam eder.

### 4.2 `HudSpring` — paylaşılan entegratör

```
public static void Step(ref Vector2 offset, ref Vector2 velocity,
                        Vector2 target, float stiffness, float damping, float dt)
{
    Vector2 accel = (target - offset) * stiffness - velocity * damping;
    velocity += accel * dt;
    offset   += velocity * dt;
}
```

Yarı-örtük Euler. Çağıran `dt`'yi `1/30` saniyeye kırpar: bir takılma karesinde büyük `dt`,
entegratörü kararsızlaştırıp HUD'ı ekrandan fırlatır.

`UnityEngine.Vector2` dışında bağımlılığı yoktur, bu yüzden istenirse EditMode'da doğrudan
ölçülebilir (bkz. 7.2).

### 4.3 İki katman

**Katman 1 — `HudRootSway`:** `SwayRoot`'a öteleme + küçük bir z rotasyonu uygular. Rotasyon,
köşelerin doğal olarak farklı yönlerde kaymasını sağlar (açısal eylemsizlik) — köşelere elle sahte
yön atamadan.

**Katman 2 — `HudSway`:** her widget kendi işaretli çarpanıyla ayrıca ötelenir. Farklı **miktarlar**
derinlik hissi verir.

Varsayılan ayar: tüm widget çarpanları **aynı işarette**, farklı büyüklükte; kök rotasyonu küçük
ama sıfırdan büyük. Sonuç fiziksel olarak dürüsttür (her şey aynı yöne geri kalır), köşelerde hafif
bir yelpazelenme hissedilir.

Ayarla ulaşılabilen diğer hisler, kod değişikliği gerektirmez:

- Yelpazeyi büyüt → `HudRootSway.maxRotationDegrees` yükselt
- Tek levha, rotasyonsuz → `maxRotationDegrees = 0`
- Bir köşeyi ters yöne gönder → o widget'ın çarpanının işaretini çevir

Her iki bileşen de `Awake`'te `rectTransform.anchoredPosition`'ı **dinlenme konumu** olarak
yakalar; sway daima bunun üstüne eklenir. Editörde yerleştirdiğin konum hiçbir zaman kalıcı olarak
bozulmaz.

Inspector alanları (hepsi Play Mode'da canlı ayarlanabilir):

- `HudCameraMotion`: `smoothing`, `maxTrackedRate`, `teleportThresholdDegrees`
- `HudSway` ve `HudRootSway`: `stiffness`, `damping`, `maxOffsetPixels`,
  `pixelsPerDegreePerSecond` (işaretli `Vector2`)
- Yalnızca `HudRootSway`: `maxRotationDegrees`

### 4.4 `CrosshairPulse`

Konumu **hiç** değiştirmez. Aynı `HudCameraMotion`'ı okur; açısal hız arttıkça 4 çentik merkezden
biraz açılır ve opaklık düşer, kamera durunca toplanır. Eylemsizlik hissi ekran merkezine de yayılır,
nişan noktası piksel piksel dürüst kalır.

## 5. Widget'lar

Her widget kaynağını **tek bir `Refresh()` metodunda** okur. Bu, 8. bölümdeki geçiş yolunun
dayanağıdır.

### 5.1 `CrosshairWidget`

Kaynak: `PlayerInteraction.CurrentPrompt`

4 çentik + merkez nokta. `CurrentPrompt` null değilken çentikler vurgu rengine döner — "buna
basabilirsin" sinyali doğrudan nişangahtan gelir.

### 5.2 `StaminaWidget`

Kaynak: `PlayerController.CurrentStamina`, `MaxStamina`, `IsSprinting`, `IsExhausted`

- Bar doluluğu = `CurrentStamina / MaxStamina`
- Sürekli görünür (klasik HUD kararı)
- `IsExhausted` iken kırmızı + yavaş nabız. `PlayerController` tükenmişliği `%30` doluluğa kadar
  kaldırmaz; oyuncunun "neden hâlâ koşamıyorum" sorusunun cevabı ekranda olmalı
- `idleAlpha` alanı mevcut ama varsayılanı `1.0` — yani varsayılan davranış "her zaman tam görünür",
  klasik HUD kararından sapmaz

### 5.3 `ObjectiveWidget`

Kaynak: `ObjectiveManager.CompletedCount`, `RequiredCount`, `RegisteredCount`,
`OnAnyFuseCompleted`, `OnAllComplete`

- Metin: `SİGORTA 1/3`
- `OnAnyFuseCompleted` → kısa parlama
- `OnAllComplete` → metin `ÇIKIŞ AÇILDI` olur ve sabit kalır

Savunma: sigorta spawner'ı henüz yazılmadı, dolayısıyla `SetRequiredCount` çağrılmamış olabilir ve
`RequiredCount` 0 gelebilir. Bu durumda payda olarak `RegisteredCount` kullanılır; `0/0` gösterilmez.

### 5.4 `InteractionPromptWidget`

Kaynak: `PlayerInteraction.CurrentPrompt`

Metin doluysa ~0.12 sn'de belirir, null olunca aynı sürede söner.

Bu, şu anda oyunda **hiç görünmeyen** tek bilgidir: `FuseBox.InteractionPrompt` "Hold E to activate
fuse" döndürür ama hiçbir yerde çizilmez, oyuncunun bir sigortayla etkileşebileceğini anlamasının
yolu yoktur.

### 5.5 `FlashlightWidget`

Kaynak: `FlashlightController.IsOn`

İkon parlak / sönük. Pil barı yok — TGD bölüm 8'de pil mekaniği tanımlı değil.

## 6. Görünürlük ve hata davranışı

### 6.1 `HudVisibility`

`GameFlowController.OnStateChanged`'e abone olur, `HudCanvas` üzerindeki tek `CanvasGroup`'un
alpha'sını kısa bir geçişle sürer.

| Oyun durumu | HUD |
|---|---|
| `InitializationState` | gizli |
| `ExplorationState` | görünür |
| `FinalChaseState` | görünür |
| `FuseInteractionState` | gizli |
| `DeathState` | gizli |
| `VictoryState` | gizli |

`FuseInteractionState`'te gizlenir çünkü `FuseMiniGameController` kendi karartma katmanını
(`darkenOverlay`) ekrana sermektedir; HUD onun üstünde asılı kalırsa panelle görsel olarak çakışır.

`SetActive` **kullanılmaz**, alpha kullanılır. Obje kapatılırsa widget'ların `Awake`'te yakaladığı
dinlenme konumları ve yay durumları sıfırlanır; HUD geri geldiğinde zıplar.

### 6.2 Hata davranışı

HUD bir görüntüleyicidir; eksik bir referans oyunu bozmamalıdır.

- Bir widget'ın kaynağı `Awake`'te null ise: obje adıyla birlikte **bir kez** `Debug.LogWarning`
  basar ve `enabled = false` yapar. Her karede `NullReferenceException` kusmaz.
- `HudSway` / `HudRootSway` `HudCameraMotion`'sız kalırsa hiçbir şey yapmaz, dinlenme konumunda
  kalır.
- Tüm olay abonelikleri `OnEnable` / `OnDisable` çiftindedir. Bu projede olay ömrü daha önce sessiz
  hataya yol açmıştır — `SceneWiring`'in sınıf yorumunun tamamı bu konudadır.

## 7. Doğrulama

### 7.1 Elle kontrol listesi

Otomatik PlayMode testi yazılmayacak (proje sahibinin açık talebi); doğrulama oynayarak yapılır.

1. Hızlı sağa-sola dön → köşeler geriden gelip yaylanarak yerine otursun, ekran dışına taşmasın
2. Crosshair merkezde piksel piksel sabit mi — dönerken yalnızca açılıp soluklaşmalı, kaymamalı
3. Koş → bar insin; bırak → ~1 sn sonra dolsun; tüket → kırmızı + nabız, `%30`'a kadar koşamama
4. Sigortaya bak → prompt çıksın; başka yöne bak → sönsün
5. Sigorta tamamla → sayaç artsın ve parlasın
6. Minigame açılınca HUD kaybolsun, kapanınca geri gelsin
7. Ölüm ve Zafer → HUD kaybolsun
8. `F` → fener ikonu değişsin
9. Yeni harita üret / oyuncuyu ışınla → HUD sarsılmasın (4.1 ışınlanma koruması)

### 7.2 Otomatik test — kapsam dışı

`HudSpring.Step` saf bir fonksiyon olduğu için EditMode'da ölçülebilir ("sonlu adımda hedefe
yakınsıyor mu"). Jam kapsamında gereksiz görüldü ve **yazılmayacak**. `HudSpring`'in ayrı ve saf
tutulmasının tek sebebi bu değil — okunabilirlik ve iki sway bileşeni arasında kod tekrarını
önlemek de aynı ölçüde geçerli.

## 8. İleriye dönük: event tabanlı HUD

Poll modeli şimdilik yeterli, ancak ileride event tabanlı bir model gerekebileceği kabul edildi.

Geçiş yolu: her widget kaynağını yalnızca kendi `Refresh()` metodunda okur. Push modeline geçmek,
widget başına o **tek metodu** değiştirmek demektir. Yerleşim, hiyerarşi, hareket modeli
(`HudCameraMotion`, `HudSpring`, `HudSway`, `HudRootSway`) ve görünürlük mantığı bu değişimden
hiç etkilenmez — hiçbiri gameplay tipleriyle konuşmaz.

## 9. Değişmezler

- Crosshair `SwayRoot` altına **asla** taşınmaz ve `HudSway` **asla** almaz (bkz. 2.1)
- `Game.Gameplay`, `Game.UI`'ı referans almaz
- HUD hiçbir gameplay durumunu değiştirmez; yalnızca okur
- Sway daima `Awake`'te yakalanan dinlenme konumunun üstüne eklenir, onu kalıcı olarak yazmaz
