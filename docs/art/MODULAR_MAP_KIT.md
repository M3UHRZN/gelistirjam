# Gloomveil Modular Map Kit — 3D Artist Görevi

## Amaç

Seed ile üretilen, minimum 2 katlı ve Inspector'dan 3–4+ kata çıkarılabilen endüstriyel korku labirenti için modüler kit hazırlanacak. Sistem matematikte 4 m'lik gizli yerleşim birimi kullanır; oyuncunun gördüğü mekân kareli defter gibi tekrarlamamalıdır.

## Ortak Ölçüler

- 1 Unity unit = 1 metre.
- Gizli yerleşim birimi: 4 × 4 m.
- Kat yüksekliği: tam 4 m.
- Koridor kullanılabilir genişliği: 2.8 m.
- Kullanılabilir tavan yüksekliği: en az 3.2 m.
- Kapı açıklığı: 1.6 m genişlik, en az 3.0 m yükseklik.
- Root pivot: footprint'in yatay merkezi, alt zemin seviyesi.
- Forward: +Z. Root scale: (1,1,1). Negatif/non-uniform scale kullanılmaz.
- Her prefab 0°, 90°, 180° ve 270° döndürülerek kusursuz birleşmelidir.
- Kuzey/doğu/güney/batı için ayrı mesh teslim edilmeyecek; aynı prefab runtime'da döndürülecek.

## P0 Prefab Listesi

### Koridorlar

| Prefab | Footprint | Canonical açık socket'ler |
|---|---:|---|
| `PF_COR_Straight_1x1_01` | 4 × 4 m | `N_00`, `S_00` |
| `PF_COR_Straight_1x2_01` | 4 × 8 m | `N_00`, `S_00` |
| `PF_COR_Straight_1x3_01` | 4 × 12 m | `N_00`, `S_00` |
| `PF_COR_Corner_1x1_01` | 4 × 4 m | `N_00`, `E_00` |
| `PF_COR_TJunction_1x1_01` | 4 × 4 m | `N_00`, `E_00`, `W_00` |
| `PF_COR_CrossJunction_1x1_01` | 4 × 4 m | `N_00`, `E_00`, `S_00`, `W_00` |
| `PF_COR_DeadEnd_1x1_01` | 4 × 4 m | `N_00` |
| `PF_COR_DoorTransition_1x1_01` | 4 × 4 m | `N_00`, `S_00` |

### Odalar

| Prefab | Footprint | Not |
|---|---:|---|
| `PF_ROOM_1x2_01` | 4 × 8 m | 90° dönüşte 8 × 4 m |
| `PF_ROOM_2x2_01` | 8 × 8 m | Dört dönüş desteklenir |
| `PF_ROOM_2x3_01` | 8 × 12 m | 90° dönüşte gerekli 12 × 8 m (`3x2`) hali oluşur |

Start, Exit, Fuse ve Safe için ayrı oda geometrisi yapılmayacak. Aynı oda kabukları aşağıdaki anchor'larla rol alacak.

### Merdivenler

| Prefab | Footprint | Alt / üst bağlantı |
|---|---:|---|
| `PF_STA_Straight_1x2_01` | 4 × 8 m | alt `S_00` (0,0,-4), üst `N_00` (0,4,+4) |
| `PF_STA_Switchback_2x2_01` | 8 × 8 m | alt `S_00` (-2,0,-4), üst `S_01` (+2,4,-4) |

Her merdiven tam 4 m yükselir. Straight merdivende tek ramp collider; switchback merdivende her kol için bir ramp collider ve düz landing collider kullanılır. Görsel basamaklar navigation collider'ı olarak kullanılmaz. Net merdiven ve landing genişliği en az 2.8 m'dir.

## Socket Sözleşmesi

Oda ve koridor socket çocukları `Socket_N_00`, `Socket_E_00`, `Socket_S_00`, `Socket_W_00` biçimindedir. Büyük odalarda aynı kenarda `..._01`, `..._02` devam eder.

Canonical `width × depth` footprint için:

- North/South socket sayısı = `width`.
- East/West socket sayısı = `depth`.
- North/South index konumu: `x = (i + 0.5 - width / 2.0) * 4`.
- East/West index konumu: `z = (i + 0.5 - depth / 2.0) * 4`.
- North/South sınırı: `z = ±depth * 2`; East/West sınırı: `x = ±width * 2`.
- Socket forward ekseni dışarı bakar.
- Her dinamik oda socket'inin aynı suffix ile `Portal_*` ve `WallCap_*` çocuğu olur.
- Stair çocukları `Socket_Bottom_<side>_<index>` ve `Socket_Top_<side>_<index>` adını kullanır.

## Anchor Sözleşmesi

Her oda:

- `Anchor_PlayerStart`
- `Anchor_Exit`
- `Anchor_Fuse_00`
- `Anchor_SafeZone`

Uygun modüllerde kullanılabilecek ortak anchor'lar:

- `StalkPoint_Corner`
- `StalkPoint_DoorSide`
- `StalkPoint_BehindCover`
- `StalkPoint_StairLanding`
- `MonsterSpawnCandidate`
- `LightAnchor`
- `PropSocket`

Merdivenlerde ayrıca `Landing_Bottom` ve `Landing_Top` bulunur.

## Görsel Yön ve Tekrarı Kırma

- Koyu endüstriyel beton/metal laboratuvar dili.
- Her 4 × 4 m birimin etrafına boyalı çerçeve veya zorunlu zemin çizgisi koyma.
- Her birimin merkezine aynı prop'u yerleştirme.
- Duvar dikişini zorunlu olarak her 4 m'de tekrarlama.
- Uzun kiriş, boru ve kablolar gizli modül sınırlarını geçiyormuş hissi vermeli.
- Tileable ortak materyaller kullan; modül başına benzersiz 4K texture üretme.
- Asimetrik hasar/kir ve dressing varyantlarıyla tekrar hissini kır.
- AI hareketi ve görüş hattı için 2.8 m net dolaşım alanını prop'larla kapatma.

## Collider ve Teslim

- Basit BoxCollider bileşimleri tercih edilir; komşu footprint'e taşma olmaz.
- Duvar, zemin ve tavan collider'larında görünmez boşluk veya üst üste binme olmaz.
- Root adı, footprint, pivot, socket/anchor transformları, layer, collider kapsamı ve mevcut `CatalogEntryId` graybox sözleşmesiyle aynı kalır.
- P1 `_02`/`_03` görsel varyantları aynı `FamilyId` altında, benzersiz `CatalogEntryId` ile gelir.

## Graybox Replacement Batch Kabulü

Graybox'lar tek seferde değil, aşağıdaki sırayla ve ayrı batch'ler halinde değiştirilir:

| Batch | Manuel kabul konfigürasyonu | Bu batch'te kontrol edilecekler |
|---|---|---|
| 1. Koridorlar | seed `1042`, 2 kat | 4/8/12 m straight çeşitliliği, corner/junction/door transition birleşimleri, portal ve wall-cap kapanışı |
| 2. Odalar | seed `2718`, 3 kat | `1×2`, `2×2`, `2×3` ve görünür `3×2` rotasyonu, role anchor'ları, odalar arası negative space |
| 3. Merdivenler | seed `9001`, 4 kat | Her komşu kat çifti için iki ayrı stair, landing anchor'ları, player ve monster boyutunda kesintisiz NavMesh traversal |

Her batch için:

1. Yalnızca o batch'in graybox prefab'ları değiştirilir; diğer iki grup sabit bırakılır.
2. `Tools/Gloomveil/Map/Validate Module Catalog` çalıştırılır ve hata kalmadan tamamlanır.
3. Tablodaki seed ve kat sayısı üretilir; modüller Unity'de 0°, 90°, 180° ve 270° dönüşleriyle görsel olarak kontrol edilir.
4. Gap, overlap, z-fighting, collider taşması, socket/anchor kayması veya NavMesh kopması varsa batch kabul edilmez.

Kabul için tüm P0 modüller snap etmeli, 90° rotasyonlarda boşluk/çakışma oluşturmamalı, socket adları eksiksiz olmalı ve NavMesh oyuncu/monster boyutunda merdivenleri kesintisiz geçebilmelidir.
