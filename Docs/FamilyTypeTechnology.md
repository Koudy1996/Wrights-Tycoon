# Technologie rodiny a typu

Nový výpočet draku (model 2) kombinuje dva neměnné technologické otisky:
rodinu F a konkrétní typ T. Motor si zachovává vlastní, nezávislé technologie.

| Parametr | Rodina | Typ |
|---|---:|---:|
| Konstrukční hmotnost trupu | 70 % | 30 % |
| Hmotnost křídla a výztuh | 60 % | 40 % |
| Ostatní konstrukční a systémová hmotnost | 20 % | 80 % |
| Odpor draku bez profilové části | 50 % | 50 % |
| Profilový odpor křídla | 75 % | 25 % |
| Vztlakový koeficient | 60 % | 40 % |
| Materiálové náklady draku | 60 % | 40 % |
| Výrobní práce draku | 50 % | 50 % |

## Výpočet

`M = wF × F + (1 − wF) × T`.

Materiály, MD, odpor a konstrukční hmotnost se indexem M dělí, vztlak násobí.
Index1,18 tedy znamená úsporu15,25%, ne18%. Vstupy pod1 zůstávají povolené.
Geometrie, profilové slidery, materiál, přesné zpracování a další existující
volby se nadále vyhodnocují obvyklým způsobem; nový časový index je nepřepisuje.

Rodina i typ uchovávají herní rok/měsíc a hodnoty dvou existujících indexů:
konstrukční/výrobní a aerodynamický. Nejde o osm nových výzkumných oborů.
Uložení hodnot zajišťuje, že pozdější změna historické tabulky nepřepočítá
schválenou technologii. Ostatní budoucí změny pravidel hry tím nejsou zamčené.

Datum je účetní měsíc schválení zahájení vývoje, nyní následující měsíc po
kliknutí na schválení. Dokončení vývoje nezmrazuje novější technologii.
Nový samostatný typ může mít novější T než rodina F; pouhá odvozená verze
dědí datum i otisk zdrojového typu.

## Hmotnostní skupiny

- Trup: existující základní structuralMass.
- Křídlo: wingMass a wingReinforcementMass; profilové násobky zůstávají jen zde.
- Ostatní: systémy křídla, nádrže a nosná konstrukce paliva, instalace a systémy
  draku; dodatečné systémy výsledného letadla získají20/80 právě jednou.
- Skutečná hmotnost paliva, posádky a motorů se tímto indexem nezmenšuje.

Starý výpočet dělil společným indexem i palivo a posádku. Model2 tuto chybu
nepřebírá, proto i při shodném datu nemusí mít totožnou hmotnost s modelem1.
Nový výpočet nerozděluje ani neopakuje existující nelineární materiálové efekty.

## Co se nemění

- Výzkumné REL/PROD bonusy rodiny a typu se nadále plně sčítají a aplikují jednou.
- Motor není součástí vážené technologie draku a nedostává další výrobní slevu.
- Spolehlivost a návrhové přetížení nemají nový časový bonus.
- Životnost, rychlost, nosnost, spotřeba a provozní cena jsou odvozené výsledky.
- Pevné přídavky navigace nedostávají nový nezávislý technologický bonus.
- Recept náhradního dílu, tovární kapacity a automatizace se nemění.

## Ukládání

Save formát15 přijímá předchozí14. Chybějící model na starší definici znamená
legacy1; již schválené starší typy se nesmějí automaticky přepočítat modelem2.
Rodinné datum lze rekonstruovat pouze z doloženého schválení v research boardu;
takový otisk je označen jako rekonstrukce. Neznámé datum se nenahrazuje dneškem.
Závazné nabídky, dodané flotilové kohorty a zaplacená WIP nejsou touto migrací
přepočítávány. Žádné uživatelské soubory se při této implementaci nemažou.

## Ověřování

`TechnologyEffectsTests`: přesné váhy, příklady, validace, fyzické hmotnosti,
odpor/vztlak a výrobní rozpisy. `TechnologyFreezeRuntimeTests`: datované
buildéry, research rebuild a plné bonusy, JSON, různé profily a druhy motorů.
`TechnologyFreezeUiTests`: schvalování, persistence, verze a prezentace.
Výsledky kompletního běhu se zaznamenávají do `Logs/Workflow`.

Dokončené ověření25.10.2026:

- Kanonický bezgrafický běh `editmode-20261025-180757.xml`:1001úspěšných,
  0selhání,54grafických testů přeskočeno.
- Kompletní grafický běh `family-type-technology-graphics-20261025.xml`:
  **1055/1055**,0selhání,0přeskočení. Z toho38případů nové technologické politiky.
- Test celé uložené hry pokrývá formáty14/15, legacy režim, přesné otisky
  i zachování placené rozpracované výroby.
- Výzkumná tabule se obnovuje před katalogy typů, takže čekající model2typ
  nezmizí kvůli dočasně neznámému stavu rodiny.
- Samostatná rodina bez uloženého typu uchovává referenční konfiguraci pro
  aktualizaci výzkumné nabídky. Technologická metadata motoru mají explicitní
  JSONkonstruktor, takže po načtení reference neztratí své uložené hodnoty.

Windows ani macOS launcher se v tomto kroku neaktualizuje.
