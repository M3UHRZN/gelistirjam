# Retro Terminal HUD Art Pass — Gloomveil

Tarih: 2026-08-08
Durum: Tasarım onaylandı; yazılı spec incelemesi bekliyor

## 1. Amaç ve sınır

Mevcut HUD'ın bilgi mimarisi ve gameplay bağlantıları çalışır durumda. Bu art pass, o davranışı
değiştirmeden HUD'ı Gloomveil dünyasına ait, okunaklı bir **Arızalı Tesis Terminali** gibi
gösterecek.

Bu belge, `2026-08-08-hud-design.md` belgesinin görsel sunum ekidir. Aşağıdaki konularda eski
belgeyi geçersiz kılar:

- Legacy `UnityEngine.UI.Text` yerine TextMeshPro kullanılması
- Harici font kullanılmaması kararı
- Widget'ların düz Image/Text görünümü ve ölçüleri
- `HudSceneBuilder` tarafından üretilen görsel alt hiyerarşi

Eski belgedeki gameplay veri kaynakları, dört köşe yerleşimi, sway modeli, oyun durumuna göre
görünürlük, hata davranışı ve assembly bağımlılık yönü geçerliliğini korur.

Kapsam içi:

- Objective, stamina, fener ve etkileşim prompt'unun terminal paneli olarak yeniden çizilmesi
- Crosshair'in aynı görsel dile taşınması
- Cascadia Mono tabanlı TextMeshPro font varlığı
- Panel içi scanline ve düşük genlikli fosfor flicker'ı
- Retro stile uygun durum metinleri ve mikro etiketler
- `HudSceneBuilder`'ın yeni görünümü tekrar üretmesi

Kapsam dışı:

- Fuse minigame arayüzü
- Menü, pause veya ayarlar arayüzü
- Gameplay kuralları ve giriş tuşları
- Yeni HUD verisi veya yeni mekanik
- Tam ekran CRT filtresi, post-process, VHS distorsiyonu veya kamera shader'ı
- HUD yerleşimini dört köşe düzeninden çıkarmak

## 2. Görsel yön

### 2.1 Ad ve dünya gerekçesi

Estetik adı: **Arızalı Tesis Terminali**.

HUD, oyuncunun taktığı modern bir hologram gibi değil; terk edilmiş tesisin hâlâ çalışan teşhis
sisteminden oyuncuya sızan bir görüntü gibi davranır. Koyu plakalar ekranı kapatmadan bilgiye zemin
verir. Fosfor yeşili çalışan sistemi, sıcak tehlike rengi ise fiziksel tükenmeyi gösterir.

### 2.2 Üç renkli ana palet

| Rol | Renk | Kullanım |
|---|---|---|
| Taban | `#06100C` | Panel zemini; çoğunlukla `%68–78` opaklık |
| Fosfor | `#79FF9B` | Metin, çerçeve, crosshair ve aktif durum |
| Tehlike | `#FF5A36` | Tükenmiş stamina ve kritik geri bildirim |

Kapalı veya ikincil durumlar yeni renk eklemez; fosfor renginin düşük alpha değerini kullanır.
Saf beyaz, gri ve amber yardımcı palet olarak kullanılmaz.

### 2.3 Tipografi

- HUD metinlerinin tamamı `TextMeshProUGUI` olur.
- Font: Cascadia Mono Regular. Font ve lisans dosyası repo içinde
  `Assets/_Project/Art/UI/Fonts/` altında tutulur.
- Üretilen TMP font asset'i aynı klasör altında yer alır; runtime'da sistem fontu aranmaz.
- Normal gövde metni minimum 18 px, ana değerler 22–24 px olur.
- Büyük harf, kısa satır ve sabit genişlik kullanılır. Uzun açıklamalar eklenmez.
- TMP Distance Field materyalinde ince outline ve düşük glow kullanılabilir; glow okunurluğu
  düşürecek ya da bloom varmış gibi genişleyecek kadar yükseltilmez.

## 3. Ortak terminal paneli

Her köşe widget'ının `Panel` adlı child'ı, aynı `TerminalPanelGraphic` bileşeniyle çizilen tek bir
görsel plakaya sahip olur. Bu sınıf `UnityEngine.UI.MaskableGraphic` türevidir, ayrı
`Assets/_Project/Code/UI/Hud/TerminalPanelGraphic.cs` dosyasında yaşar ve kendi mesh'inde şunları
üretir:

- Yarı saydam taban dolgusu
- 1–2 px fosfor çerçeve
- Sağ üst ve sol alt kırpılmış köşe
- İki kısa, parlak kayıt işareti
- Panel sınırlarının içinde 4 px aralıklı, çok düşük alpha scanline'lar

Panel sprite veya özel shader istemez. Mesh yalnızca geometri ya da serialized görsel ayar
değiştiğinde yeniden kurulur. Hafif flicker, mesh'i her kare yeniden üretmek yerine
`CanvasRenderer.SetAlpha` üzerinden çalışır:

- Genlik en fazla `%2.5`
- Deterministik iki sinüs dalgası; rastgele ani kararma yok
- Metin flicker yapmaz, yalnızca panel zemini ve çerçevesi nefes alır
- Crosshair bu efekti almaz

`TerminalPanelGraphic.raycastTarget` daima `false` olur.

## 4. Widget tasarımları

### 4.1 Objective — sol üst

Yaklaşık ölçü: `460 × 66`, güvenli alan marjı korunur.

```
┌ GRID://POWER ─────────────┐
│ SİGORTA // 01/03           │
└────────────────────────────┘
```

- `GRID://POWER` düşük alpha fosfor mikro etikettir.
- Değer metni `SİGORTA // {0:00}/{1:00}` olur.
- Tüm hedefler tamamlanınca `ÇIKIŞ // AÇIK` gösterilir.
- Sigorta tamamlanma flash'ı fosfor alpha/parlaklık darbesidir; tehlike rengi kullanılmaz.
- `ObjectiveManager` yoksa bütün panel gizlenir. Boş terminal çerçevesi ekranda kalmaz.

### 4.2 Stamina — sol alt

Yaklaşık ölçü: `320 × 62`.

```
┌ RUN://CAPACITY ────────────┐
│ █ █ █ █ █ █ █ █ ░ ░ ░ ░  │
└────────────────────────────┘
```

- Mevcut sürekli `fillAmount` korunur.
- Fill'in üstündeki 11 ince taban rengi ayırıcı, barı 12 terminal hücresine böler.
- Normal dolgu fosfor rengidir.
- Tükenmiş durumda dolgu tehlike rengine geçer ve mevcut 1 Hz nabzı sürdürür.
- Barın kritik hareketi yalnızca renk ve alpha ile verilir; yeni ekran sarsıntısı eklenmez.

### 4.3 Fener — sağ alt

Yaklaşık ölçü: `230 × 62`.

```
┌ AUX://LIGHT ───────────────┐
│ [F] FENER          AÇIK    │
└────────────────────────────┘
```

- Tuş kapsülü statik `[F]` metnidir; buton değildir.
- Durum `AÇIK` veya `KAPALI` olarak ayrı TMP alanında gösterilir.
- Açık durumda fosfor tam parlaklıktadır; kapalı durumda aynı rengin düşük alpha hali kullanılır.
- Pil göstergesi eklenmez.

### 4.4 Etkileşim prompt'u — orta alt

Yaklaşık ölçü: en fazla `720 × 52`; merkezde gameplay'i kapatmayacak kadar aşağıda kalır.

```
┌ INPUT://CONTEXT ─────────────────────┐
│ > Hold E to activate fuse            │
└──────────────────────────────────────┘
```

- `PlayerInteraction.CurrentPrompt` içeriği değiştirilmez veya ayrıştırılmaz.
- Görünen metin `> {0}` formatıyla yazılır; böylece mevcut tuş bilgisi iki kez gösterilmez.
- Mevcut yaklaşık `0.12 sn` fade korunur.
- Prompt yokken panel ve metin birlikte tamamen görünmez olur.

### 4.5 Crosshair — sabit merkez

- `FixedRoot` altında kalır; `HudSway` almaz.
- Merkez noktası `3 × 3` elmas olur; dönüş yalnızca görsel child'a uygulanır ve merkez koordinatı
  değişmez.
- Dört ince fosfor çentik korunur. Hızlı kamera hareketinde yalnızca mevcut spread/alpha davranışı
  çalışır.
- Etkileşilebilir hedefte crosshair rengi yeni bir renk yerine fosforun tam parlaklığına çıkar;
  idle durumda alpha düşüktür.
- Panel, scanline veya flicker almaz. Nişan doğruluğu eski spec'teki değişmezdir.

## 5. Hiyerarşi ve bileşen sınırları

Ana hiyerarşi değişmez; yalnızca widget altları zenginleşir:

```
HudCanvas
├── SwayRoot
│   ├── ObjectiveWidget
│   │   ├── Panel (TerminalPanelGraphic)
│   │   ├── Header (TMP)
│   │   └── Value (TMP)
│   ├── StaminaWidget
│   │   ├── Panel (TerminalPanelGraphic)
│   │   ├── Header (TMP)
│   │   └── Bar (Background, Fill, 11 Segments)
│   ├── FlashlightWidget
│   │   ├── Panel (TerminalPanelGraphic)
│   │   ├── Header (TMP)
│   │   ├── Key (TMP)
│   │   ├── Label (TMP)
│   │   └── State (TMP)
│   └── InteractionPromptWidget
│       ├── Panel (TerminalPanelGraphic)
│       ├── Header (TMP)
│       └── Prompt (TMP)
└── FixedRoot
    └── CrosshairWidget
        ├── DotDiamond
        └── TickUp/Down/Left/Right
```

Yeni runtime bileşeni yalnızca `TerminalPanelGraphic` olur. Widget veri bileşenleri mevcut
sorumluluklarını korur; `Text` alanları `TMP_Text` olur. `FlashlightWidget`, durum metnini sürmek
için ayrı bir `TMP_Text` referansı alır. `ObjectiveWidget`, eksik kaynakta tüm paneli gizlemek için
widget `CanvasGroup` referansı alır.

`Game.UI.asmdef` ve `Game.Editor.asmdef`, `Unity.TextMeshPro` assembly referansını ekler.
Bağımlılık yönü yine yalnızca `Game.UI → Game.Gameplay` olur; gameplay assembly'sine UI referansı
eklenmez.

## 6. Builder ve varlık davranışı

`Tools/Gloomveil/UI/Build HUD` menüsü yeni hiyerarşinin tek üretim noktası olmaya devam eder.

- Builder önce Cascadia Mono TMP font asset'ini doğrular.
- Gerekli font bulunamazsa hata verir ve mevcut `HudCanvas`'ı silmez.
- Gereksinimler doğrulandıktan sonra eski HUD'ı yeniden üretir; tekrar çalıştırmak duplicate bırakmaz.
- Canvas `1920 × 1080`, Match `0.5`, Screen Space Overlay kalır.
- `GraphicRaycaster` eklenmez.
- Tüm `Graphic.raycastTarget` değerleri `false`, tüm HUD `CanvasGroup.blocksRaycasts` değerleri
  `false` olur.
- Builder'ın gameplay referansı bulma ve bağlama davranışı değişmez.
- Scene içindeki tüm TMP ve Graphic referansları serialized olarak atanır; runtime isim araması
  yapılmaz.

## 7. Hareket, durum ve veri akışı

Gameplay veri akışı değişmez:

- Widget'lar mevcut gameplay kaynaklarını yalnızca okur.
- Objective event abonelikleri eşlenmiş `OnEnable/OnDisable` çiftinde kalır.
- Diğer durumlar mevcut `Refresh()` polling akışını korur.
- `HudVisibility`, oyun state'lerine göre bütün HUD CanvasGroup'unu sürmeye devam eder.
- `HudCameraMotion`, `HudRootSway`, `HudSway` ve `CrosshairPulse` matematiği değişmez.

Görsel durum değişiklikleri widget'ın kendi alanıyla sınırlıdır. Terminal paneli gameplay nesnesini
bilmez; yalnızca serialized stil değerlerini çizer.

## 8. Hata davranışı ve erişilebilirlik

- Eksik gameplay kaynağı oyunu durdurmaz; bir kez warning üretir ve ilgili widget gizlenir.
- Eksik font, builder için geri döndürülemez bir yarım sahne üretmek yerine erken hata sebebidir.
- Flicker genliği düşük tutulur; tam ekran parlama, ani siyah kare veya hızlı strobe yoktur.
- Metin kontrastı panel üzerinde korunur ve bilgi yalnızca renkle aktarılmaz: fenerde metin,
  objective'de sayı, stamina'da doluluk biçimi vardır.
- 16:9 dışındaki çözünürlüklerde anchor ve CanvasScaler davranışı korunur; panel metinleri taşarsa
  font küçültmek yerine builder ölçüleri düzeltilir.

## 9. Doğrulama

Otomatik PlayMode veya EditMode testi yazılmayacak ve çalıştırılmayacak. Doğrulama iki katmanlıdır.

Statik/Editör doğrulaması:

1. Unity compile hatası sıfır olmalı.
2. `MainGame` içindeki yeni HUD hiyerarşisi ve serialized referanslar kontrol edilmeli.
3. `HudCanvas` üzerinde `GraphicRaycaster` bulunmamalı.
4. Crosshair `FixedRoot` altında olmalı ve hiçbir `HudSway` almamalı.
5. Game View'da panel sınırları, TMP fontu, safe-area marjları ve merkez açıklığı görsel olarak
   incelenmeli.
6. Git diff yalnızca bu art pass'in doküman, font/lisans, UI kodu ve sahne değişikliklerini
   içermeli.

Proje sahibinin Play Mode kontrolü:

1. Kamera dönerken köşe HUD'ları sway yapmalı; crosshair merkezde kalmalı.
2. Stamina 12 hücre görünümünde akmalı; tükenince tehlike rengine ve nabza geçmeli.
3. Fener aç/kapa durum metni anında güncellenmeli.
4. Prompt panel ve metin olarak birlikte fade olmalı; kaynak metin değişmemeli.
5. Objective bulunan sahnede sayı ve tamamlanma metni güncellenmeli; bulunmayan sahnede boş panel
   görünmemeli.
6. Minigame, ölüm ve zafer state'lerinde HUD görünürlüğü eski davranışı korumalı.

## 10. Değişmezler

- Retro görünüm gameplay davranışını değiştirmez.
- Crosshair ekran merkezinden ayrılmaz.
- HUD hiçbir input yakalamaz veya raycast engellemez.
- Tam ekran CRT/post-process efekti eklenmez.
- Fuse minigame bu işte değiştirilmez.
- `Game.Gameplay`, `Game.UI` assembly'sini referanslamaz.
- Harici UI paketine veya DOTween'e yeni bağımlılık eklenmez; TextMeshPro Unity UI paketinin mevcut
  parçası olarak kullanılır.
