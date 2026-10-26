# Flotila, servis, životnost, reputace a kapacita továren

Stav: implementace schválena uživatelem a zavedena do herního kódu. Dne 19. 10. 2026 prošla kompletní Unity sada 410/410; probíhá fáze 2 (ekonomika, nehodovost, UI). Audit zachování původní ziskovosti zatím nevyhovuje, viz [výsledky](Balance/FleetProfitabilityAudit-20261019.md). Vizuální kontrola zůstává neověřená. Aktuální servisní receptury a ceny, výsledky a otevřené otázky jsou v [FleetLifecycleImplementation](FleetLifecycleImplementation.md). Tento dokument nahrazuje předchozí návrh tří servisních programů. Výslovné požadavky uživatele jsou závazné; číselné výchozí hodnoty označené jako návrh jsou ladicí parametry, nikoliv historická fakta.

## 1. Přijaté požadavky

- Servisní interval je v nalétaných hodinách: 500, 1 000, 2 000 a další logické skoky. Pro první implementaci použít 500 / 1 000 / 2 000 / 4 000 / 8 000 h. Katalog je rozšiřitelný stejným pravidlem; tento výběr představuje hlavní servis, nikoliv úplný reálný předpis všech drobných kontrol.
- Výrobce vyrábí náhradní díly a náhradní motory, inkasuje servisní příjmy a řeší omezené kapacity. Existuje možnost pokračovat v podpoře typu i po zastavení výroby.
- Podpora je garantovaná na pevnou dobu; UI agreguje závazky podle typu/verze a po najetí na nejzazší datum ukáže graf.
- Očekávaná životnost je parametr stroje, ovlivněný zejména spolehlivostí a předepsaným servisem. Nový slider 0–10 ji zvýší za vyšší hmotnost, cenu a odpor.
- Kontrakty požadují životnost a obsahují servisní a provozní parametry. Většina upřednostňuje celkové provozní náklady, menšina pořizovací cenu.
- Flotila ubývá náhodnými nehodami a postupným vyřazováním po smluvní životnosti. Riziko zohledňuje historické období, spolehlivost, servis a překročení technické i smluvní životnosti.
- Reputace ovlivňuje ochotu civilních i státních zákazníků platit. Velikost budovy určuje měsíční limit MD; automatizace zvyšuje produktivitu.

## 2. Pojmy a vlastnictví

Zákazníci vlastní provozovaná letadla. Hráč je výrobce a poskytovatel servisní podpory. V první verzi servis zajišťuje agregovaná síť zákaznických dílen: továrny vyrábějí díly/motory a provádějí generálky, vlastní servis má samostatnou pracnost a přímý náklad zahrnutý v ceně. Nestavět další servisní budovy v této etapě.

| Hodnota | Jednotka a význam | Lze změnit po dodání? |
| --- | --- | --- |
| Očekávaná technická životnost | Hodiny při deklarovaném servisním programu, bez odečítání náhodných nehod | Historický odhad při prodeji je neměnný; aktuální stav se vyvíjí |
| Požadovaná smluvní životnost | Minimální počet provozních hodin požadovaný zákazníkem | Ne, uložit do smluvního snímku |
| Provozní opotřebení | Kumulovaná spotřeba technické životnosti, poměr 0 až více než 1 | Roste provozem; běžná údržba neomlazuje drak |
| Servisní interval | Hodiny mezi hlavními servisy | Pro nové nabídky ano; existující zákazníci drží sjednanou variantu |
| Garantovaná podpora | Kalendářní konec závazku na dodanou dávku | Pouze prodloužení, nebo výslovné předčasné porušení se sankcí |
| Odhad provozních nákladů | Peníze za hodinu a celkem za smluvní provoz | Nabídka se zamkne při přijetí; skutečné náklady evidovat zvlášť |

Kontrakt stanoví délku podpory (generované varianty 5 / 10 / 15 / 20 let); hráč ji NEVOLÍ jako parametr letadla. Nová nabídka, kalkulace provozních nákladů a každá skutečná dodávka používají přesně smluvní délku. Doba běží od skutečného předání každé dávky, nikoliv od podpisu nebo prvního vyrobeného kusu. Konec kalendářního měsíce je jednoznačná hranice včetně posledního garantovaného měsíce. Deset let není univerzální hodnota všech smluv. Dříve uzavřené garance v uložených hrách se touto změnou nezkracují.

## 3. Životnost a slider konstrukční odolnosti

Slider nazvat **Konstrukční odolnost**, celočíselně 0–10, ve vývoji typu. Nepřekrývat existující Precision Engineering ani Reliability research points. Typový parametr se propíše do společného výpočtu výkonu a nákladů před výpočtem rychlosti, doletu a spotřeby, aby zákaznické náklady zahrnuly i penalizace slideru. Zobrazení: odhad životnosti v h, přepočet na roky při konkrétním náletu, změna hmotnosti/ceny/odporu.

Výchozí ladicí model, bez nároku na reálnou certifikační metodu:

`L = 30 000 h × Frel(R) × Fservice(interval) × (1 + 0.08 × odolnost)`

Od 25. 10. 2026: pro R <= 1 platí `Frel = max(0.5, (R / 0.80)^1.5)`; pro R > 1 platí `Frel = (1 / 0.80)^1.5 * (1 + 1.5 * (R - 1))`. Nad 100 % pokračuje životnost lineárně bez horního stropu, se shodnou hodnotou i sklonem v bodě R=1. Všechny návrhy mají společný základ 30 000 h a referenční spolehlivost 80 %, nezávisle na tržním zařazení. Použít výslednou spolehlivost včetně motorových bonusů a součtu bonusů rodiny a typu. Při prodeji zamknout specifikaci kohorty.

`Fservice = clamp((1000 / interval)^0.2, 0.65, 1.2)`; častější servis má omezený přínos. Požadavky kontraktů a reference nehodovosti zůstávají beze změny. Změna nepřidává závislost na herním roce a nepřepočítává zmrazené specifikace již dodané flotily. Příklad: spolehlivost 80 %, servis 500 h a odolnost 5 dávají přibližně 48 245 h.

Návrh penalizací při hodnotě 10: +20 % konstrukční suché hmotnosti, +35 % dotčených konstrukčních dílů a konstrukčních MD, +8 % konstrukčního odporového koeficientu. Nezdražovat znovu nakoupené motory, palivo ani už jednou započtenou práci. Výsledná cena, payload, rychlost a spotřeba se přepočítají existujícím modelem. Žádný přímý dodatečný násobitel životnosti za slider v nehodovém modelu: jeho účinek se přenáší přes L.

Odhad L není slib bezporuchového provozu ani měsíční pravděpodobnost přežití. Technickou způsobilost nabídky kontrolovat `L >= smluvní minimum` při požadovaném servisním programu. Přeslibování není povolená cesta k vyšší ceně.

## 4. Registr flotily a skutečný servis

Kohorta sdružuje pouze skutečně shodné kusy: typ a verzi, kontrakt/dodací dávku, měsíc dodání, náletový a rizikový profil, servisní fázi, opotřebení, slíbenou životnost a konec podpory. Agregace UI může být hrubší; simulace nesmí slít odlišné závazky. Uchovat původní počet, aktivní a odstavené kusy, kumulované nehody a běžná vyřazení. Vyřazené kusy se archivují a netvoří servisní poptávku ani příjmy.

Každá úspěšná civilní i státní dodávka vytvoří kohortu právě jednou se stabilním DeliveryId. Skladové kusy nejsou provozní flotila. Částečné státní dodávky tvoří samostatné dávky. Zrušený obchod/vrácený sklad nevytváří flotilu.

Provozní profil drží plán hodin za měsíc a průměr hodin na let; skutečně odlétané hodiny respektují odstávky a postupné vyřazení. V každém měsíci vyhodnotit všechny dosažené servisní prahy, ne pouze boolean „servis byl potřeba“. Například 2 500 h provozu od nuly při intervalu 500 h vyžaduje pět servisů. Bez dodání potřebného servisu nevynulovat čítač: evidovat dluh a omezení dostupnosti. U dlouhého měsíčního náletu rozdělit výpočet na úseky mezi servisními a životnostními prahy.

Dokončený servis spotřebuje díly, práci a případně výměnný motor; teprve pak se zaúčtuje příjem. Při nedostatku zásob se provede jen obsloužitelná část a kohorta se podle potřeby rozdělí. Nesmí existovat záporné sklady ani duplicitní platby. Opravitelné motory putují do fronty generálek, po dokončení zpět do zásoby; poškozené neopravitelné kusy odepsat. Výměna motoru nemění stáří ani nalétané hodiny draku. Kompatibilita dílů je pro první verzi přesná rodina/verze dle explicitní kompatibilní skupiny; žádná automatická kompatibilita všech motorů.

Základní opotřebení za úsek je `dW = skutečné hodiny / L × Fnedodržení`. Fnedodržení je 1 při splněném předepsaném servisu a roste při dluhu; nenásobit jej znovu bonusem za nominální interval, který už je v L. Dokončení běžného servisu zastaví dodatečné opotřebení, ale nesmaže minulost.

Po skončení garance může hráč dál prodávat podporu. Ukončení po splnění závazků a s oznámením není automatické provinění; předčasné ukončení aktivních garancí nese explicitní smluvní náklad a dopad reputace. Zastavení výroby/archivace návrhu nesmí odstranit katalog potřebný pro servis.

## 5. Nehody a postupné vyřazování

Historická evidence a její limity jsou v [FleetAccidentResearch.md](FleetAccidentResearch.md). Profil volit podle provozu a kategorie, ne pouze podle pístového/proudového motoru. Nepoužívat stejnou základnu pro sportovní letadlo, linkový dopravní letoun a vojenský výcvik. Bojové ztráty nejsou náhodné provozní nehody; nejsou součástí této etapy.

Návrh mechanismu: základní intenzita rozdělená na provozní složku a technickou složku. Historická celková četnost už obsahuje typickou spolehlivost a údržbu doby; modifikátory proto normalizovat vůči referenci daného období/profilu, nikoliv přidat celé technické riziko podruhé.

`mu = E × [lambda_provoz + lambda_technika × Freliability × Fservis × (1 + a × max(0, W-1)^2 + b × max(0, H/Lcontract-1)^2)]`

E je skutečná expozice: hodiny / 100 000, nebo lety / 1 000 000 podle zdrojové řady. H je nálet draku a Lcontract smluvní životnost. a,b jsou herní koeficienty, ne historicky doložené účinky. Provozní složka zůstává i u velmi spolehlivého letadla. Neodvozovat lambda ze vztahu `1 - reliability`.

Pravděpodobnost pro daný kus za úsek je `p = 1 - exp(-mu)`. Ztráty losovat binomicky z aktuálních kusů, s odděleným deterministickým proudem náhody. U zdroje všech nehod použít explicitní model podílu neopravitelných poškození; fatální nehoda ani běžná nehoda nejsou synonymem ztráty stroje. Evidence rozlišuje příčinu a závažnost, pro flotilu je rozhodující počet ztracených letadel. Případné vícestrojové události jsou v první verzi zjednodušení jednotlivých ztrát, výslovně označit v kalibraci.

Postupný konec života: pracovní návrh pevně rozdělit kusům při dodání prahy běžného vyřazení mezi 100–120 % smluvní životnosti. Začínají odcházet po dosažení Lcontract, do 120 % odejde zbývající běžná flotila. Prahy/hodinové podkohorty přetrvají save/load. Tento ocas umožňuje krátký provoz nad smluvním limitem a odpovídající zvýšené riziko, nikoliv neomezenou servisní rentu. Předčasné dosažení W=1 zvyšuje technické riziko a může vést k odstavování; normální smluvní odchody nezapočítávat jako nehody.

Pořadí v úseku: z provozovaných kusů určit hodiny/expozici do prahu, uplatnit nehody, běžně vyřadit pouze přeživší překračující svůj práh, uzavřít stav. Jeden kus může mít jen jednu terminální příčinu. Archiv drží příčiny a datum/hodiny pro účetnictví a reputaci.

## 6. Kontrakty a ekonomika provozu

Civilní i státní kontrakty a historicky/obchodně vkládané nabídky dostanou: MinimumExpectedLifeHours, MaximumServiceIntervalHours, MinimumSupportMonths, UtilizationHoursPerMonth, AverageFlightHours, OperatingProfileId, ProcurementPreference a nákladový limit. Konkrétní dodávka navíc drží sjednaný interval, servisní tarif, požadovanou životnost, délku podpory a neměnný kalkulační snímek. Rozlišit požadavky, nabízené vlastnosti a skutečné výsledky.

Návrh mixu nových kontraktů: 25 % důraz na pořizovací cenu, 75 % důraz na náklady celého provozu; poměr konfigurovat podle kategorie/éry a aplikovat na oba kanály. Oba profily kontrolují minimální životnost a podporu. Levná nabídka tak neobejde technickou způsobilost.

Cena za celý smluvní provoz = kupní cena + palivo při smluvním náletu + plánované servisy + očekávané opravy/výměny motorů + ocenění odstávek. V první verzi bez diskontování a bez zůstatkové hodnoty. Kalkulace je deterministický očekávaný výsledek, nepouští náhodnou simulaci pro každé překreslení UI. Zahrnuje celou požadovanou životnost, nikoliv jen kratší garanci; po garanci pokračuje standardní tarif sjednaný v nabídce pro účely odhadu. Nejasná budoucí dostupnost podpory má vlastní rizikovou přirážku, nikoliv nulový náklad.

Palivo = skutečná existující spotřeba kg/h × hodiny × cena/kg příslušného paliva. Nekombinovat cenu/litr se spotřebou kg bez hustoty. Cena paliva je zákaznický náklad, nikoliv příjem výrobce. Servisní tarif zahrnuje díly, práci a zahrnuté výměny motoru; pokud je motor zvlášť placený, vyjmout ho z balíčku. Nedvojit materiálové výnosy s cenou kompletního servisu.

Návrh ochoty platit: reputace 0–100, start 50, násobitel 0.85–1.15. Aplikovat právě jednou na obchodní cenový strop; u provozně orientované zakázky stále platí limit celkových nákladů. Reputace neodpouští nesplněné minimum životnosti, servisní interval ani garanci a nemění již uzavřené ceny. U státních zakázek oddělit vývojovou cenu/financování od ceny sériové dodávky a servisu; násobitel neaplikovat na dotaci několikrát.

## 7. Továrny a sdílené MD

První realizace zachová existující organizaci továren a zařízení; přidá kapacitu budovy, fronty náhradních dílů/motorů/generálek a prioritní přidělování MD. Nezavádět současně nový systém dopravy dílů ani zcela nový editor výrobních hal.

`MD dostupné = min(kapacita budovy, personál × 21)`

`MD zakázky = základní MD × stávající automatizační násobitel`

Návrh velikostí budov: 2 100 / 8 400 / 33 600 MD měsíčně. Pořizovací náklady, provoz a délka rozšíření jsou samostatné ladicí tabulky. Automatizace nemění limit budovy, neuplatnit zároveň rychlostní bonus i snížení stejných MD. Nahrazení současných limitů 10/25/50/75/100 kusů výpočtem MD; při zachování jiného kusového stropu doložit konkrétní výrobní důvod.

Rozpočet MD zahrnuje i výrobu motorů pro nové letouny, kterou současný model dopočítává automaticky. Vyřešit ji buď přiřazením stávajícího implicitního motorového provozu do explicitní kapacity, nebo započtením celé kombinované pracnosti výrobku do přiděleného závodu; pro první verzi zvolit druhou možnost a uvést zvlášť drak/motory v reportu. Žádná skrytá neomezená motorová továrna pro spare motory.

Výchozí priorita: garantovaný servis -> přijaté dodávky -> dobrovolná podpora -> výroba zásob; hráč může prioritu změnit a uvidí předpověď porušení. Fronty mohou produkovat rozpracovanost při jednotce větší než měsíční kapacita. Přenášet jen MD skutečně odpracované na konkrétním výrobku, nikoliv nevyužitou kapacitu. Zvlášť reportovat obsazení fyzické kapacity a využití dostupného personálu.

## 8. Reputace a srozumitelné UI

Reputace má jedno viditelné skóre s rozpisem příčin, za měsíc agregované a omezené změny. Odlišit technickou příčinu, nesplněný servis výrobce a externí provozní okolnosti. Zohlednit expozici a minimální statistickou důvěru, aby jeden náhodný případ v malé flotile nezničil firmu a velká flotila nebyla trestána absolutním počtem. Běžné stáří/vyřazení není reputační trest. Vyšší cena servisu sama o sobě negeneruje reputaci; plnění závazků ano.

Přehled flotily: jeden řádek na typ/verzi, aktivní/odstavené kusy, průměrný nálet a podíl spotřebované životnosti, servisní backlog, měsíční výnos/marže, nejzazší garantovaná podpora. U závazků navíc drobný indikátor nejbližší expirace a počet právě ohrožených strojů. Maximum data se počítá z neukončených záruk včetně vyřešení posledního kusu, historie zůstává v detailu.

Na hover/focus nad datem podpory zobrazit graf garantovaných počtů kusů v čase (12/60 měsíců a celá doba), počet aktivních smluv, nejbližší a poslední konec. Vyznačit odhad servisní zátěže a datum možného ukončení podpory. Kliknutí připne detail pro pohodlné čtení; nespoléhat pouze na hover. Výpočet grafu cacheovat, nepřepočítávat celou flotilu při každém OnGUI. Jeden měsíční souhrn a jen důležité výstrahy, nikoliv dialog na každý servis či každý kontrakt.

Designer: předepsaný servis a konstrukční odolnost vedle životnosti, tooltip s dopady a referenčním náletem. Kontrakty: viditelně preference zákazníka, kupní cena, palivo/h, servis/h, celkové náklady, životnost a garance. Factory: stoh kapacity nové stroje/díly/motory/generálky a výhled 12 měsíců. Company: historie reputace a konkrétní příčiny.

## 9. Hranice implementace a migrace

Zjištěný stav: SavePayloadVersion = 11 v LaunchRunner; skutečný herní měsíční krok orchestruje AdvanceToNextMonth v témže souboru. Obecný MonthlySimulationEngine existuje, ale nelze předpokládat, že řídí tento herní tok. Nové čisté služby zapojit do skutečného orchestrace, bez plošného přepisování celé simulace.

Doménová logika patří do Core (M3/M5/M6/M7/M9 a nové Fleet/Reputation složky pod Assets/Modules), nikoliv do OnGUI. Core nepřidává závislost na Unity. Sdílená kalkulace životnosti/provozu musí obsloužit preview, oba evaluátory kontraktů i kohorty. Nové moduly mohou využít stávající WrightTycoon.Core.asmdef; nové samostatné assembly nejsou podmínka.

Navrhované snímky: AircraftLifecycleSpecification, OperatingProfile, ServiceOfferSnapshot, FleetCohortSnapshot, SupportObligationSnapshot, SpareInventorySnapshot, WorkOrderSnapshot, ReputationSnapshot a RNG snapshot. Při novém save použít další volnou verzi podle skutečného stavu při implementaci; nepředpokládat slepě, že stále půjde o verzi 12.

Staré návrhy: odolnost 0, předepsaný servis 1 000 h jako kompatibilní výchozí stav. Staré aktivní kontrakty dokončit dle původních pravidel bez zpětného přidání životnostních podmínek; nové kontrakty už používají nový model. Z minulých prodejů neodhadovat celou flotilu jen z TotalAircraftSold. Kde chybí historické kohorty, zahájit registr a garance od aktivace změny a označit to v přehledu; žádné nečekané zpětné dluhy, nehody ani servisní příjmy.

Kapacity existujících budov migrovat jako explicitní LegacyCapacityMd alespoň na úroveň dosavadního nastaveného měsíčního zatížení při migraci. Nové rozšíření/prodej používá nové ekonomické tabulky. Evidovat původ kvůli transparentnosti, nevytvářet novou výchozí velikost při každém load. Chybějící save data nesmějí přepsat existující referenční snímky nebo vynulovat náhodný proud.

## 10. Pořadí měsíčního zpracování a akceptace

1. Zachytit otevírací stav a profily již dodaných kohort; stanovit servisní potřeby a úlohy pro měsíc.
2. Rozdělit konečné výrobní MD a dokončit dostupné díly, motory, generálky a nové stroje.
3. Zpracovat servis a letové úseky existujících kohort v pořadí prahů; zapsat dluh, odstávky, skutečnou expozici, nehody a vyřazení.
4. Úspěšné nové dodávky zaregistrovat s datem předání; nemají zpětný celý měsíc provozu. Dodávka provedená kliknutím mimo měsíční krok se také registruje atomicky.
5. Jednou uzavřít servisní účetnictví a reputaci, potom nově oceňované nabídky; již přijaté podmínky zůstávají zamčené. Zachovat stávající časování expirací a vlády tam, kde ho změna nevyžaduje.
6. Vyhodnotit cash flow, úvěry a bankrot až po všech servisních nákladech i výnosech. Uložit snímek a agregované reporty.

Výsledek musí projít scénářem: nabídka -> přijetí -> výroba -> částečná/dokončená dodávka -> provoz -> několik servisů -> shortage -> reputace -> nehoda/životnostní odchod -> konec podpory -> save/load uprostřed. Fixní seed a stejná rozhodnutí dávají stejné výsledky bez závislosti na počtu překreslení UI. Počty a peníze se musí účetně uzavřít.

Implementační soubory jsou v [.codex/plans](../.codex/plans/fleet-lifecycle-README.md). Součástí dokončení je kompletní EditMode workflow dle [TestingAndLauncherWorkflow.md](TestingAndLauncherWorkflow.md); buildy až při požadavku na aktualizaci launcherů.
