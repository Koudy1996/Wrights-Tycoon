# Modulární profily křídla

Herní model, nikoliv měřené poláry skutečných profilů. Jména NACA/NASA jsou skutečná referenční označení. Přípona `modified` říká, že herní geometrie a parametry jsou upravené, nikoliv totožné s dokumentovaným profilem. Rychlost je vždy návrhová rychlost ve výšce **0 m**, nikoliv tvrdý limit letadla. Mezi číselnými body se lineárně interpoluje; reference se vybírá podle nejbližšího rychlostního bodu. Změna názvu nemění plynulost vlastností.

Rodina ukládá tři parametry: rychlost 100–2 000 km/h (UI krok 50), nákladní ↔ akrobatický −100 až +100 (krok 5), účinnost 0–100 (krok 5). Výchozí nastavení: 350 / 0 / 50. Výsledný název má například formát `NACA 23012 — modified`; rychlost, zaměření a kvalita výroby jsou samostatné hodnoty, nikoliv součást standardizovaného označení.

## Rychlostní základ a názvy

| Rychlost km/h | Název řady | Inspirace | CLmax | Profilový CD0 | Tloušťka | Hmotnost konstrukce křídla | Cena / MD křídla |
|---:|---|---|---:|---:|---:|---:|---:|
| 100 | STOL | NACA 4418 | 2,10 | 0,0200 | 18 % | ×1,10 | ×1,05 |
| 200 | Utility / Užitkový | NACA 4415 | 1,85 | 0,0140 | 15 % | ×0,95 | ×0,95 |
| 350 | Universal / Univerzální | NACA 23012 | 1,60 | 0,0100 | 12 % | ×1,00 | ×1,00 |
| 500 | Cestovní | NACA 63-412 | 1,40 | 0,0080 | 12 % | ×1,05 | ×1,15 |
| 750 | Rychlostní | NACA 65-210 | 1,20 | 0,0075 | 10 % | ×1,15 | ×1,35 |
| 1 000 | Transonický | NASA SC(2)-0410 | 1,05 | 0,0085 | 8 % | ×1,30 | ×1,65 |
| 1 400 | Nadzvukový | NACA 64A204 | 0,85 | 0,0100 | 5 % | ×1,55 | ×2,10 |
| 2 000 | Vysokorychlostní | NACA 64A003 | 0,65 | 0,0120 | 3 % | ×1,90 | ×2,80 |

## Nosnost a obratnost

Přehled názvů pro skutečné kroky rychlostního slideru (čísla se stále interpolují, nikoli skokově přepínají):

| Nastavená rychlost | Zobrazená řada |
|---|---|
| 100–150 km/h | NACA 4418 — modified |
| 200–250 km/h | NACA 4415 — modified |
| 300–400 km/h | NACA 23012 — modified |
| 450–600 km/h | NACA 63-412 — modified |
| 650–850 km/h | NACA 65-210 — modified |
| 900–1 200 km/h | NASA SC(2)-0410 — modified |
| 1 250–1 700 km/h | NACA 64A204 — modified |
| 1 750–2 000 km/h | NACA 64A003 — modified |

Při zaměření obratnosti alespoň +75 a návrhové rychlosti do 600 km/h se místo této tabulky používá symetrická reference `NACA 0012 — modified`. Účinnost názvosloví nemění: jde o provedení a výrobní kvalitu, ne jinou standardizaci geometrie. Kategorie STOL/cestovní apod. jsou jen vysvětlení použití v této dokumentaci, nikoliv názvy v UI.

| Hodnota | Přívlastek | CLmax | CD0 | Hmotnost křídla | Cena / MD | Návrhové přetížení |
|---:|---|---:|---:|---:|---:|---:|
| −100 | Cargo / Nákladní | ×1,25 | ×1,15 | ×0,90 | ×0,95 | 2,5 g |
| −50 | Transport / Dopravní | ×1,12 | ×1,07 | ×0,95 | ×0,97 | 3,0 g |
| 0 | Universal / Univerzální | ×1,00 | ×1,00 | ×1,00 | ×1,00 | 3,8 g |
| +50 | Agile / Obratný | ×0,94 | ×0,98 | ×1,10 | ×1,12 | 4,8 g |
| +100 | Aerobatic / Akrobatický | ×0,88 | ×1,00 | ×1,25 | ×1,30 | 6,0 g |

## Účinnost

| Hodnota | Provedení | CD0 | Hmotnost křídla | Cena / MD | CLmax | Návrhové přetížení |
|---:|---|---:|---:|---:|---:|---:|
| 0 | Jednoduché | ×1,20 | ×1,12 | ×0,75 | ×0,90 | ×0,90 |
| 25 | Ekonomické | ×1,10 | ×1,06 | ×0,87 | ×0,95 | ×0,95 |
| 50 | Standardní | ×1,00 | ×1,00 | ×1,00 | ×1,00 | ×1,00 |
| 75 | Optimalizované | ×0,90 | ×0,96 | ×1,25 | ×1,025 | ×1,025 |
| 100 | Precizní | ×0,80 | ×0,92 | ×1,60 | ×1,05 | ×1,05 |

Nižší účinnost nemá žádnou přímou technickou výhodu: křídlo je těžší, má větší odpor, menší vztlak i nižší návrhové přetížení. Jediná výhoda je nižší cena a méně výrobní práce. Motory, palivo a existující spolehlivost nemají nový přímý násobek; dolet a jiné odvozené výsledky se nadále počítají z celého letadla.

## Zapojení a omezení

- Výsledné profilové koeficienty jsou součinem rychlostního základu a interpolovaných násobků. CL dále respektuje polohu/tvar křídla, klapky a technologii. V modulárním režimu se neuplatňuje starý strop CL=1,8 ani staré enumové bonusy.
- Hmotnostní násobek působí pouze na existující hmotnost křídla a jeho výztuh. Nezvyšuje přímo palivo, posádku, motor ani trup. Konstrukční, materiálové a technologické faktory se dále uplatní standardním způsobem.
- Podíl křídla pro cenu a MD = `(základní hmotnost křídla + výztuhy) / (základní konstrukce + křídlo + výztuhy + systémy křídla)`. Používá se referenční hmotnost před profilovým násobkem. Příplatek či úspora = základ výrobních nákladů po materiálu/konstrukci × podíl křídla × `(profilový násobek − 1)`. V rozpisu má vlastní řádek; ostatní doplňky zůstávají beze změny.
- Obratnostní skóre po ostatních faktorech násobí `sqrt(návrhové přetížení / 3,8)`, poté zachovává dosavadní rozsah 1–100. Jde o herní skóre ovlivněné plošným zatížením, nikoliv simulaci udržitelného přetížení v dané rychlosti.
- Profilový odpor je samostatný koeficient vztažený na plochu křídla. Základní odpor ostatních částí zůstává vztažený na čelní plochu. Účinnost tedy nezlepšuje odpor podvozku či trupu. Čistota, přesné zpracování a technologické bonusy se uplatňují i na profilový odpor.
- Jen profilová část se při překročení návrhové rychlosti násobí `1 + 4 × max(0; V/Vdesign − 1)²`: při +10 % ×1,04; +25 % ×1,25; +50 % ×2; +100 % ×5. Pod návrhovou rychlostí je násobek 1. V je skutečná rychlost TAS; návrhová rychlost je pevná reference definovaná v 0 m a s výškou se neposouvá. Hustota vzduchu a tah motoru se s výškou dále mění obvyklým způsobem.
- Reference drag area v reportu je součet statického body-CD × čelní plocha + profilové CD × plocha křídla; ReferenceDragAtSeaLevel100Ms navíc respektuje rychlostní penalizaci při 360 km/h.
- Staré programové fixtures mohou používat `UseModularWingProfile=false`; hráčské rodiny nový režim zapínají. Staré savy nejsou podporovanou migrační cestou této aktualizace.

Příklad 350 / −100 / 75: `NACA 23012 — modified`; CLmax profilu 2,05; CD0 0,01035; hmotnost křídla ×0,864; cena/MD křídla ×1,1875; přetížení 2,5625 g. Jde o profil před dodatečnými vlastnostmi celého draku.

## Zdroje názvosloví

- [NASA/OpenVSP: standardizace čtyř-, pěti- a šestkové řady NACA](https://www.nasa.gov/reference/openvsp-cross-sections/).
- [UIUC Airfoil Coordinates Database: skutečná označení a geometrie profilů](https://m-selig.ae.illinois.edu/ads/coord_database.html).
- [NASA TP-2969: Supercritical Airfoils, SC(2)-0410](https://ntrs.nasa.gov/api/citations/19900007394/downloads/19900007394.pdf).
- [NASA TM-85937: používané NACA 64A204 a 64A003](https://ntrs.nasa.gov/api/citations/19840014468/downloads/19840014468.pdf).

Například NACA 4418 kóduje 4% prohnutí, jeho polohu ve 40% hloubky a 18% tloušťku. NACA 0012 je symetrický, 12% tlustý. Standardizované číslo není CLmax ani rychlostní limit. Hodnoty herní varianty (včetně interpolované tloušťky) zůstávají herní kalibrací a mohou se od reference lišit.
