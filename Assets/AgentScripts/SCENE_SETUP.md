# Sahne Kurulumu ve Component Rehberi

Bu dosya, ML-Agents drone egitim sahnesinin nasil kurulacagini adim adim anlatir.
Tum karakterler ASCII uyumlu yazildi; Unity Inspector disindaki editorlerde dogru gorunur.

---

## 1. Sahne Hierarchy Yapisi

```
Scene
|-- Main Camera
|-- Directional Light
|-- Terrain / Ground          (Tag: Ground, Layer: Default)
|-- World Objects             (engeller, Tag: World Object)
|
|-- AgentTrack                (Track kok objesi)
|   |-- CP_0                  (Kontrol noktasi 0)
|   |-- CP_1                  (Kontrol noktasi 1)
|   |-- CP_2
|   |-- CP_3
|   |-- _GeneratedWaypoints   (Runtime'da otomatik olusur)
|       |-- Waypoint_000
|       |-- Waypoint_001
|       |-- ...
|
|-- TrainingManager           (Drone spawner)
|   |-- TrainingDrone_000     (Runtime'da olusur)
|   |-- TrainingDrone_001
|   |-- ...
|
|-- TimeScaleManager
|
|-- SpawnPoint                (opsiyonel bos GameObject)
```

---

## 2. Project Settings Ayarlari

### 2.1. Tags (Edit > Project Settings > Tags and Layers > Tags)
Asagidaki tag'lerin var oldugundan emin olun:
- `Player`
- `Ground`
- `World Object`

### 2.2. Layers (ayni pencerede Layers kismi)
Bos bir User Layer secin (ornek: User Layer 8) ve adini yazin:
- `TrainingDrone`

### 2.3. Physics Collision Matrix
Edit > Project Settings > Physics > en alttaki Layer Collision Matrix:

- `TrainingDrone` x `TrainingDrone`  =>  kutucuk BOS (capraz isareti yok)
- `TrainingDrone` x `Default`        =>  kutucuk ISARETLI (zeminle carpisir)
- `TrainingDrone` x digerleri        =>  kutucuk ISARETLI (varsayilan)

Bu ayar drone'larin birbirleriyle fizikzel temasini keser, ama zeminle/engellerle
carpismayi acik tutar.

---

## 3. Component Detaylari

### 3.1. `AgentTrack` (bos GameObject)

Component: **Transform**
- Position: parkurun merkezi olacak pozisyon

Component: **AgentTrackGenerator**
- Default Waypoints Per Segment: 3
- Default Curve Depth: 0
- Default Waypoint Scale: (3, 3, 1)
- Waypoint Prefab: (bos birakabilirsiniz, otomatik cube olusur)
- Default Waypoint Color: yesil
- Draw Gizmos: ACIK
- Gizmo Curve Samples: 24

---

### 3.2. Kontrol Noktalari `CP_0, CP_1, ...` (AgentTrack'in child'lari)

Her biri bos GameObject. AgentTrack altina Right-Click > Create Empty ile eklenir.

Component: **Transform**
- Position: parkurun o noktasindaki konum

Component: **AgentTrackControlPoint**
- Place Waypoint Here: ACIK (ise yaramali)
- Use Segment Scale: ACIK
- Waypoint Scale Override: (3, 3, 1)  [Use Segment Scale kapaliysa kullanilir]
- Waypoints To Next: 3 - 5 arasi (segmentte kac ara waypoint)
- Curve Depth: 0 (duz) veya 3-5 (viraj)
- Curve Direction: (0, 1, 0) yukari, (1, 0, 0) yanlara
- Segment Waypoint Scale: (3, 3, 1)
- Gizmo Color: her CP icin farkli renk (scene'de ayirt etmek icin)

Not: Son CP'de Waypoints To Next ve Curve Depth kullanilmaz (sonrasinda segment yok).
     Ama Place Waypoint Here isareti son CP'de de o noktaya waypoint koyup koymayacagini belirler.

---

### 3.3. `AgentDrone.prefab` (drone prefab'i)

Component: **Transform**
- (normal)

Component: **Rigidbody**
- Mass: 1               (AgentDroneController bunu override eder)
- Use Gravity: ACIK
- Is Kinematic: KAPALI
- Interpolate: Interpolate
- Collision Detection: Continuous
- Constraints: hicbiri

Component: **Collider** (BoxCollider, CapsuleCollider veya MeshCollider)
- Is Trigger: KAPALI
- ONEMLI: collider drone'un child'inda olmali (govde/mesh objesinde)
- Child objenin Tag'i: `Player`

Component: **MeshFilter + MeshRenderer**
- Gorsel icin drone modeli

Component: **AgentDroneController**
```
Physics Settings:
  Mass: 1
  Max Thrust Scale: 2
  Max Torque: 20
  Yaw Torque: 10
  Linear Drag: 0.5
  Angular Drag: 2

Episode Settings:
  Max Tilt Before Reset: 45
  Max Expected Distance: 100
  Max Altitude: 50

Spawn Settings:
  (Manager otomatik doldurur)

Sparse Rewards:
  Waypoint Reward: 1.0
  Completion Bonus: 2.0
  Crash Penalty: -1.0
  Tilt Penalty: -1.0
  Timeout Penalty: -0.5
  Below Ground Penalty: -1.0

Dense Rewards:
  Distance Reward Scale: 0.02
  Alignment Reward Scale: 0.01
  Time Penalty: 0.001
```

Component: **Behavior Parameters** (Agent scripti eklendiginde otomatik gelir)
- Behavior Name: DroneAgent
- Vector Observation:
    Space Size: 17
    Stacked Vectors: 1
- Actions:
    Continuous Actions: 4
    Discrete Branches: 0
- Behavior Type: Default
- Model: bos (egitimden sonra .onnx dosyasi atanir)
- Team ID: 0

Component: **Decision Requester**
- Decision Period: 5
- Take Actions Between Decisions: ACIK

Agent Ayari (Max Step, AgentDroneController'in Inspector'inin ust kisminda):
- Max Step: 5000

Prefab'da Layer ve Tag:
- Root GameObject Layer: Default (Manager runtime'da TrainingDrone yapar)
- Collider child Tag: Player (tekli test icin prefab'da ayarli olabilir,
  yoksa Manager otomatik ayarlar)

---

### 3.4. `TrainingManager` (bos GameObject)

Component: **Transform**
- Position: drone'larin spawn olmasini istediginiz nokta
  (veya SpawnPoint kullanin)

Component: **AgentTrainingManager**
- Drone Prefab: AgentDrone.prefab surukleyip birakin
- Drone Count: 20 (baslangic icin; 50-100 egitimi hizlandirir)
- Shared Track: AgentTrack objesi
- Spawn Point: SpawnPoint objesi (bosta TrainingManager pozisyonu kullanilir)
- Drone Layer Name: TrainingDrone

---

### 3.5. `SpawnPoint` (opsiyonel bos GameObject)

Component: **Transform**
- Position: drone'larin spawn olacagi konum
- Rotation: drone'larin baslangic yonu (genelde track'in ilk segmentine dogru)

Bu objenin hic baska componenti olmasina gerek yok.

---

### 3.6. `TimeScaleManager` (bos GameObject)

Component: **Transform**
- (normal)

Component: **AgentTimeScaleManager**
- Time Scale: 1 (ilk egitimde 1, stabil oldugunda 10-20)
- Auto Adjust Fixed Delta Time: ACIK
- Base Fixed Delta Time: 0.02

---

### 3.7. Zemin (Ground)

Kullandiginiz Terrain veya Plane objesinde:
- Tag: Ground
- Layer: Default
- Collider: var, Is Trigger KAPALI

---

### 3.8. Engel Objeleri

Sahnedeki her engel icin:
- Tag: World Object
- Collider: var, Is Trigger KAPALI

---

## 4. Kurulum Sirasi (Adim Adim)

1. Project Settings > Tags and Layers > Layers'a `TrainingDrone` ekleyin
2. Project Settings > Physics > Collision Matrix'te TrainingDrone self-collision'i kapatin
3. Zemin/engelleri sahneye yerlestirin ve tag'leyin
4. AgentTrack bos GameObject'ini olusturun, AgentTrackGenerator ekleyin
5. AgentTrack altina CP_0, CP_1, CP_2, CP_3 child'lari ekleyin,
   her birine AgentTrackControlPoint ekleyin
6. AgentTrackGenerator bileseninde Right-Click > Generate Track secin,
   scene'de waypoint'lerin olustugunu dogrulayin
7. AgentDrone prefab'ini olusturun:
   - Rigidbody + Collider + AgentDroneController
   - Behavior Parameters (Space Size 17, Continuous Actions 4)
   - Decision Requester (period 5)
   - Max Step 5000
8. TEKLI TEST: prefab'i sahneye birakin, Track Generator'u referansla,
   Play'e basin ve Heuristic modda klavye ile test edin
   (W/S throttle, ok tuslari pitch/roll, A/D yaw)
9. Test drone'unu silin
10. TrainingManager bos GameObject olusturun, AgentTrainingManager ekleyin,
    tum referanslari verin
11. TimeScaleManager bos GameObject olusturun, AgentTimeScaleManager ekleyin
12. Play'e basin - tum drone'lar ayni noktada spawn olmali,
    paralel olarak track'i kosmali, birbirlerine degmemeli

---

## 5. Sik Karsilasilan Hatalar ve Cozumleri

| Problem | Cozum |
|---------|-------|
| Drone'lar birbirine carpiyor | Layer Collision Matrix'te TrainingDrone self-collision kapali mi? |
| Waypoint tetiklenmiyor | Drone'un child collider'i Player tag'li mi? Is Trigger False mu? |
| Track scene'de gorunmuyor | Scene view'da Gizmos butonu acik mi? Generate Track calistirildi mi? |
| Space Size 17 degil hatasi | Behavior Parameters > Vector Observation Space Size = 17 yapin |
| Drone hover edemiyor | Max Thrust Scale dusuk veya Rigidbody Mass cok yuksek |
| Drone spawn anti crash atiyor | Spawn Point yerden yeterince yukseklige alin (orn: 5m) |
| Episode hemen bitiyor | Max Tilt Before Reset cok dusuk olabilir (45+ olmali) |
| Training baslamiyor | mlagents-learn calistirip SONRA Unity'de Play'e basin |

---

## 6. Egitim Komutlari Ozeti

```
# Python ortami (ilk kurulum)
pip install mlagents

# Egitimi baslat
mlagents-learn config/drone_training.yaml --run-id=drone_run_01

# Simdi Unity'de Play'e basin

# TensorBoard ile izle (yeni terminal)
tensorboard --logdir results

# Egitimi devam ettir (crash sonrasi)
mlagents-learn config/drone_training.yaml --run-id=drone_run_01 --resume

# Egitilmis modeli kullan
# results/drone_run_01/DroneAgent.onnx dosyasini
# Behavior Parameters > Model alanina surukleyin
# Behavior Type: Inference Only yapin
```
