# ML-Agents Drone Training — Kurulum Rehberi

## Mimari Özet

Tek bir track/parkur üzerinde onlarca drone aynı anda, üst üste, iç içe koşar. Her drone bağımsız episode'lar çalıştırır. Birbirlerini fiziksel olarak görmezler (physics layer izolasyonu). Tüm drone'lar aynı policy'yi öğrenir.

---

## 1. Tag ve Layer Oluşturma

### Tag'ler
**Edit > Project Settings > Tags and Layers > Tags** kısmına gidin:

- `Player` — (muhtemelen zaten var) Waypoint trigger tespiti için
- `Ground` — Zemin objeleri
- `World Object` — Engeller

> `AgentDrone` tag'ine artık gerek **yok** — drone'lar birbirini görmüyor.

### Layer Oluşturma
**Edit > Project Settings > Tags and Layers > Layers** kısmına gidin:

1. Boş bir slot bulun (ör: User Layer 8)
2. Adını **`TrainingDrone`** yapın

### Physics Collision Matrix Ayarı
**Edit > Project Settings > Physics** kısmına gidin:

1. En altta **Layer Collision Matrix** tablosunu bulun
2. `TrainingDrone` satırı ile `TrainingDrone` sütununun **kesişim kutucuğunun işaretini kaldırın**
3. Bu, drone'ların birbirleriyle çarpışmasını kapatır
4. Drone'lar hâlâ `Default`, `Ground`, `World Object` vb. layer'larla çarpışmaya devam eder

> **Not**: Script bunu runtime'da da otomatik yapar ama editor'da kalıcı olarak ayarlamak daha güvenli.

---

## 2. Waypoint Prefab Oluşturma (Opsiyonel)

Eğer özel bir prefab kullanmak istiyorsanız:

1. Sahneye bir **Cube** ekleyin
2. **BoxCollider > Is Trigger = true** yapın
3. **AgentWaypoint** scriptini ekleyin
4. Prefab olarak kaydedin: `Assets/Prefabs/AgentWaypoint.prefab`

> Prefab atamazsanız, `AgentTrackGenerator` otomatik olarak görünür yeşil cube'lar oluşturur.

---

## 3. Track (Parkur) Oluşturma

### Temel Yapı
1. Sahneye boş bir **GameObject** oluşturun → adını `AgentTrack` koyun
2. **AgentTrackGenerator** scriptini ekleyin
3. `AgentTrack` altına **boş child GameObject'ler** ekleyin — bunlar kontrol noktaları:
   - `CP_0`, `CP_1`, `CP_2`, `CP_3`, ... şeklinde adlandırın
   - Hierarchy'deki sıralama = parkur sırası (yukarıdan aşağıya)

### Per-Segment Özelleştirme
Her kontrol noktasına **AgentTrackControlPoint** bileşeni ekleyin. Bu sayede:

| Ayar | Açıklama |
|------|----------|
| **Place Waypoint Here** | Bu CP'nin tam konumuna waypoint koyulsun mu? |
| **Waypoints To Next** | Bu CP ile bir sonraki arasında kaç ARA waypoint (0 = hiç) |
| **Curve Depth** | Bu segmentin eğriliği (0 = düz çizgi, 5+ = derin viraj) |
| **Curve Direction** | Eğrinin bükülme yönü. (0,1,0) = yukarı, (1,0,0) = yana |
| **Segment Waypoint Scale** | Bu segmentteki waypoint'lerin boyutu |
| **Waypoint Scale Override** | CP'deki waypoint için özel boyut (Use Segment Scale kapalıysa) |

**Örnek**:
- `CP_0`: `placeWaypointHere = true`, `waypointsToNext = 3`, `curveDepth = 0` (düz başlangıç)
- `CP_1`: `placeWaypointHere = true`, `waypointsToNext = 5`, `curveDepth = 4` (derin viraj)
- `CP_2`: `placeWaypointHere = false`, `waypointsToNext = 2`, `curveDepth = 0` (CP'de waypoint yok)

### Track'i Oluşturma ve Önizleme
- **Inspector'da AgentTrackGenerator bileşenine sağ tıklayın → "Generate Track"**
- Waypoint'ler sahneye yerleşir ve Scene view'da gizmo olarak görünür
- Değişiklik yaptıktan sonra tekrar "Generate Track" çalıştırın
- Silmek için: sağ tıklayın → **"Clear Track"**
- Scene view'da `Gizmos` butonunun **açık** olduğundan emin olun

---

## 4. Drone Agent Prefab Oluşturma

1. Mevcut drone prefab'ınızın bir **kopyasını** oluşturun
2. Kopyadan şu scriptleri **kaldırın**: `DroneController`, `CameraController`, `GuideLineVisualizer` (agent bunları kullanmaz)
3. Şu bileşenlerin kalmasını sağlayın: `Rigidbody`, `Collider`(lar)
4. **AgentDroneController** scriptini ekleyin
5. **Behavior Parameters** (otomatik eklenir):
   - **Behavior Name**: `DroneAgent`
   - **Vector Observation > Space Size**: `17`
   - **Actions > Continuous Actions**: `4`
   - **Actions > Discrete Branches**: `0`
6. **Decision Requester** ekleyin:
   - **Decision Period**: `5`
   - **Take Actions Between Decisions**: `true`
7. **Max Step** ayarlayın (Agent bileşeninde): önerilen `5000` - `10000`
8. Prefab olarak kaydedin: `Assets/Prefabs/AgentDrone.prefab`

> **NOT**: Layer ve tag'ları prefab'da ayarlamayın — `AgentTrainingManager` bunları otomatik yapar.

---

## 5. Tekli Drone ile Test (İlk Adım)

Çoklu drone'a geçmeden önce tek drone'la her şeyin çalıştığını doğrulayın:

1. Sahneye `AgentDrone` prefab'ını yerleştirin
2. **AgentDroneController** bileşeninde **Track Generator** alanına `AgentTrack` objesini sürükleyin
3. Drone'un layer'ını `TrainingDrone` yapın (Inspector > Layer)
4. Drone'un child collider'larını `Player` olarak tag'leyin
5. Play'e basın — **Heuristic** modda (klavye ile) test edin:
   - W/S = throttle, Ok tuşları = pitch/roll, A/D = yaw
6. Waypoint'lerden geçebildiğinizi, çarpışmada episode'un bittiğini doğrulayın

---

## 6. Çoklu Drone Eğitim Ortamı Kurulumu

1. Sahneye boş bir **GameObject** oluşturun → adını `TrainingManager` koyun
2. **AgentTrainingManager** scriptini ekleyin
3. Inspector'da ayarları yapın:

| Alan | Değer | Açıklama |
|------|-------|----------|
| **Drone Prefab** | `AgentDrone.prefab` | 4. adımda oluşturduğunuz prefab |
| **Drone Count** | 20-100 | Paralel drone sayısı |
| **Shared Track** | `AgentTrack` | Sahnedeki tek track objesi |
| **Spawn Point** | (opsiyonel) | Drone'ların başlangıç noktası. Boşsa `TrainingManager`'ın pozisyonu kullanılır |
| **Drone Layer Name** | `TrainingDrone` | 1. adımda oluşturduğunuz layer adı |

4. Eğer sahnede 5. adımdaki test drone'u varsa **silin** — `AgentTrainingManager` kendi drone'larını oluşturur

> **Nasıl çalışır**: Tüm drone'lar aynı noktada spawn olur, aynı waypoint'lerden geçer. Physics layer sayesinde birbirlerini görmezler. Her drone kendi episode'unu bağımsız yönetir.

---

## 7. TimeScale Manager

1. Sahneye boş bir **GameObject** → adını `TimeScaleManager` koyun
2. **AgentTimeScaleManager** scriptini ekleyin
3. Inspector'da:
   - **Time Scale**: 1x ile başlayın, eğitim stabil olunca 10-20x'e çıkarın
   - **Auto Adjust Fixed Delta Time**: Açık bırakın

---

## 8. ML-Agents YAML Konfigürasyonu

Projenin kök dizininde `config/drone_training.yaml`:

```yaml
behaviors:
  DroneAgent:
    trainer_type: ppo
    hyperparameters:
      batch_size: 2048
      buffer_size: 20480
      learning_rate: 3.0e-4
      beta: 5.0e-3
      epsilon: 0.2
      lambd: 0.95
      num_epoch: 3
      learning_rate_schedule: linear
    network_settings:
      normalize: true
      hidden_units: 256
      num_layers: 3
    reward_signals:
      extrinsic:
        gamma: 0.99
        strength: 1.0
    max_steps: 5000000
    time_horizon: 512
    summary_freq: 10000
    keep_checkpoints: 5
    checkpoint_interval: 100000
```

> **Not**: Çok sayıda drone kullanıyorsanız `batch_size` ve `buffer_size`'ı artırın. 50+ drone için `batch_size: 4096`, `buffer_size: 40960` deneyin.

---

## 9. Eğitimi Başlatma

### Gereksinimler
- Python 3.8-3.10
- `pip install mlagents`

### Komutlar

```bash
# Eğitimi başlat
mlagents-learn config/drone_training.yaml --run-id=drone_run_01

# Unity'de Play'e basın

# TensorBoard ile izle
tensorboard --logdir results

# Eğitimi devam ettir
mlagents-learn config/drone_training.yaml --run-id=drone_run_01 --resume
```

---

## 10. Eğitim İpuçları

1. **Aşamalı yaklaşım**:
   - 1. aşama: 1 drone, TimeScale 1x → hover + ilk waypoint'e ulaşma doğrula
   - 2. aşama: 20 drone, TimeScale 5x → stabil öğrenme
   - 3. aşama: 50+ drone, TimeScale 10-20x → hızlı eğitim

2. **Waypoint boyutu**: Başlangıçta büyük (`5, 5, 2`), öğrendikçe küçültün.

3. **Ödül ayarlama** (TensorBoard `Environment/Cumulative Reward` izleyin):
   - Sürekli negatif → `crashPenalty` azaltın veya `distanceRewardScale` artırın
   - Plato → `waypointReward` veya `completionBonus` artırın
   - Drone yere iniyor → `belowGroundPenalty` artırın
   - Çok yavaş → `timePenalty` artırın

4. **MaxStep**: Inspector'da `Max Step = 5000-10000` ayarlayın. Çok düşükse agent parkuru bitiremez.

5. **Drone sayısı vs performans**: 50+ drone = daha hızlı öğrenme ama daha fazla RAM/GPU. Unity profiler ile izleyin.

---

## Ödül Tablosu (Özet)

### Sparse (Episode Sonu)
| Durum | Ödül | Tetikleyici |
|-------|------|-------------|
| Waypoint geçme | +1.0 | OnTriggerEnter, aktif waypoint |
| Parkur tamamlama | +2.0 bonus | Son waypoint |
| Çarpışma (zemin/engel) | -1.0 | OnCollisionEnter |
| Aşırı eğim (>45°) | -1.0 | FixedUpdate kontrol |
| İrtifa < 0 | -1.0 | FixedUpdate kontrol |
| Zaman aşımı | -0.5 | MaxStep |

### Dense (Her Adım)
| Ödül | Açıklama |
|------|----------|
| Mesafe azaltma | Yaklaştıysa +, uzaklaştıysa - |
| Hız yönü hizalama | Hedefe doğru uçma ödülü |
| Zaman cezası | Sabit -0.001 |

---

## Dosya Yapısı

```
Assets/
├── AgentScripts/
│   ├── AgentDroneController.cs      — ML-Agents drone agent (per-drone waypoint tracking)
│   ├── AgentWaypoint.cs             — Paylaşımlı waypoint (state-agnostic, event-based)
│   ├── AgentTrackGenerator.cs       — Kontrol noktalarından waypoint üretimi (per-segment ayarlar)
│   ├── AgentTrackControlPoint.cs    — Per-CP segment ayarları (curve, scale, count)
│   ├── AgentTrainingManager.cs      — Tek track üzerinde çoklu drone spawn + layer izolasyonu
│   ├── AgentTimeScaleManager.cs     — Simülasyon hızı kontrolü
│   └── WALKTHROUGH.md               — Bu dosya
├── Scripts/                          — Mevcut scriptler (DOKUNULMADI)
└── Prefabs/
    └── AgentDrone.prefab             — Oluşturmanız gereken prefab
```
