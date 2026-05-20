# Waypoint Tetiklenmiyor — Tani Rehberi

Drone waypoint icinden gecti ama tepki yok (carpma sayilmiyor, odul verilmiyor,
episode bitmiyor). Asagidaki adimlari SIRAYLA takip edin.

---

## Adim 0: Debug Log'larini Acin

1. Sahnedeki waypoint objesini secin.
2. Inspector'da **AgentWaypoint > Debug > Debug Log** kutucugunu ISARETLEYIN.
3. Sahnedeki drone objesini secin.
4. Inspector'da **AgentDroneController > Debug > Debug Waypoints** kutucugunu ISARETLEYIN.
5. Play'e basin. Console'da log'lari izleyin.

Log'lardaki mesajlara gore asagidaki senaryolardan birine denk gelecek.

---

## Senaryo A: Console'da HIC "[AgentWaypoint]" log'u yok

Waypoint'in `OnTriggerEnter` hic calismiyor. Demek ki drone waypoint'e fiziksel
olarak degmemis veya collider'lar carpismiyor.

### Kontrol listesi:
- [ ] Waypoint'in **BoxCollider**'i var mi?
- [ ] Waypoint BoxCollider'inda **Is Trigger** ISARETLI mi?
- [ ] Waypoint'in ebati (Scale) dronun icinden gecebilecegi buyuklukte mi?
      Kucuk bir kup `(1,1,1)` olabilir, drone da boyle kucukse ya ucak degisiyor.
      `(3, 3, 1)` normal boyut.
- [ ] Drone'un collider'i var mi? (Rigidbody olan root objesinde veya child'da)
- [ ] Drone Rigidbody'de **Is Kinematic** KAPALI mi?
- [ ] Physics Layer Collision Matrix'te drone'un layer'i ile waypoint'in layer'i
      arasindaki kutucuk ISARETLI mi? (genelde ikisi de Default olur, carpisir)

### Hizli test:
Waypoint'in Inspector'inda collider'in Size'ini `(10, 10, 10)` yapin.
Drone'u waypoint'in ortasindan dogrudan gecirin. Hala log yoksa collider veya layer sorunu.

---

## Senaryo B: "[AgentWaypoint] REJECTED: tag 'X', expected 'Player'"

Drone'un collider'i `Player` tag'ine sahip degil.

### Cozum:
Dronun **collider'inin oldugu** GameObject'in Tag'ini `Player` yapin.

ONEMLI: Root GameObject (Rigidbody olan) degil, **collider'in uzerinde oldugu**
GameObject. Cogu drone prefab'inda collider bir child'dadir.

Ornek hierarchy:
```
DroneRoot (Rigidbody, AgentDroneController, Tag=Untagged)
 |-- Body (MeshRenderer, BoxCollider, Tag=Player)  <-- BU Player tag'li olmali
 |-- RotorFL (gorsel, collider yok)
```

Eger drone prefab'inda collider root'ta ise, root'u `Player` yapin.

---

## Senaryo C: "[AgentWaypoint] REJECTED: Wrong direction"

Drone waypoint'e ters yonden giriyor. Direction check (dot product) reddediyor.

### Cozum 1 (test icin):
Waypoint Inspector'inda **Ignore Direction Check** ISARETLEYIN. Bu direction
kontrolunu kapatir — sadece test icin, egitime baslamadan once kapatmayin.

### Cozum 2 (duzeltme):
Waypoint'in rotasyonunu duzeltin. Waypoint'in **transform.forward** (mavi ok)
drone'un gecis yonune bakmalidir.

Scene view'da waypoint secili iken mavi ok'un drone'un gelis yonunun
**aksi yone** bakmasi GEREKIR. Yani dron Z+ dogrultusunda ucuyorsa,
waypoint'in forward'i da Z+ yonunde olmali.

---

## Senaryo D: "[AgentWaypoint] ACCEPTED. Firing event to 0 subscriber(s)"

Trigger calisiyor ama hicbir drone controller subscribe olmamis.
**EN YAYGIN sebep bu.**

### Olasi sebepler:

#### D1: AgentDroneController'da Track Generator referansi bos
Inspector > AgentDroneController > References > Track Generator alani bos.

**Cozum:** Hierarchy'deki AgentTrack objesini surukleyip bu alana birakin.

#### D2: Waypoint, AgentTrackGenerator'in listesinde degil
Waypoint'i manuel olarak sahneye koydu iseniz (prefab surukleyerek veya
baska bir yontemle), AgentTrackGenerator ondan haberdar degildir.

**Cozum:** Waypoint'i silin. AgentTrackGenerator'in Inspector'inda **sag tik
> Generate Track** secin. Waypoint'ler otomatik olusturulacak.

ALTERNATIF: Waypoint'i kendi kendinize eklemek istiyorsaniz,
AgentTrackGenerator scriptini duzenlemeniz gerekir (liste manuel destek).

#### D3: AgentDroneController.Initialize() cagirilmadi
Eger ML-Agents paketi dogru kurulmadi veya Behavior Parameters eksikse
`Initialize()` cagrilmaz.

**Cozum:**
- Package Manager > Unity Registry > ML Agents kurulu olmali
- Prefab'da **Behavior Parameters** bileseni olmali
- **Decision Requester** bileseni olmali

---

## Senaryo E: "ACCEPTED" gorunuyor ama "SKIPPED: currently targeting #X" uyarisi

Waypoint subscribe olmus ve tetiklenmis ama drone baska bir waypoint'i hedefliyor.

Bu **dogru davranis**: drone waypoint'leri SIRAYLA gecmeli. Eger 3 waypoint
varsa #0 -> #1 -> #2 sirasiyla gecilmeli, rastgele atlanamaz.

### Tek waypoint'li test ediyorsaniz:
- currentWaypointIndex 0 ile basliyor
- Waypoint index 0 olmali
- AgentTrackGenerator generate ettiyse otomatik olarak index 0 olur

Log'da `currently targeting #0` ve `waypoint #0` esitse sorun yok, PASSED cikmaliydi.
Eger PASSED cikmiyor ama SKIPPED cikiyorsa index yanlis.

### Ayrica test:
Waypoint'in Inspector'inda debug log acikken "waypointIndex" gorunmuyor cunku
`[HideInInspector]`. Dogrulamak icin Inspector ust saginda **3 nokta menusu >
Debug** modunu secin, gorunur olur.

---

## Senaryo F: Hicbir log cikmadi, herhangi bir tuse de basiyorum

Muhtemelen Behavior Parameters > Behavior Type = **Heuristic Only**
yapilmamis. Ajana aksiyon gelmiyor, `Heuristic()` cagirilmiyor.

### Cozum:
1. Drone prefab'i secin
2. Inspector > **Behavior Parameters > Behavior Type = Heuristic Only**
3. Tekrar Play'e basin

Hala yoksa **Decision Requester** ekleyin (yoksa).

---

## Hizli Dogrulama Komutu

Drone'un durumu hakkinda bir defalik log almak icin sahne Play mod'da iken
Console ust saginda filtre `AgentDroneController` yazin ve Initialize log'una
bakin. `Subscribed to N waypoints` gorunmeli.

Eger `trackGenerator reference is NULL` gorunuyorsa D1.
Eger `has ZERO waypoints` gorunuyorsa D2.

---

## Checklist Ozet (hizli erisim)

1. Waypoint BoxCollider + Is Trigger = true
2. Drone collider child'inda Tag = Player
3. AgentDroneController > Track Generator = AgentTrack (bos birakma)
4. AgentTrack > sag tik > Generate Track (manuel waypoint koyma)
5. Behavior Parameters > Behavior Type = Heuristic Only (manuel test icin)
6. Decision Requester bileseni var
7. Waypoint forward yonu drone'un gecis yonu ile ayni
8. Test icin direction check'i kapatabilirsiniz (Ignore Direction Check)
