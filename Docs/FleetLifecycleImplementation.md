# Implementace životního cyklu flotily

Aktualizace 25. 10. 2026: bonusy vývoje se uplatňují na skutečné výsledné reporty motorů; bonusy rodiny a typu se sčítají. Životnost nad 100% spolehlivostí roste lineárně bez stropu. Save payload má verzi 13. Podrobnosti a pravidla ochrany zmrazených dat jsou v [ResearchBonusPropagation](ResearchBonusPropagation.md); níže uvedený stav z 19. 10. je historický záznam.

Stav k 19. 10. 2026: implementace, ekonomický audit a opravy smluvní podpory/UI fáze 2 v pracovním stromu. Kompletní Unity EditMode sada prošla 419/419 a oba zkušební launchery jsou aktualizované. **Audit aktuálního modelu nevyhovuje požadavku zachovat původní ziskovost**; 168/180 scénářů stejné flotily je mimo orientační ±10% pásmo. Viz [audit ziskovosti](Balance/FleetProfitabilityAudit-20261019.md). Nejde o vyvážený ani vizuálně ověřený distribuční build. Zadání: [FleetLifecycleDesign](FleetLifecycleDesign.md), podklady: [FleetAccidentResearch](FleetAccidentResearch.md).

## Co je zapojeno

- Typ letadla: konstrukční odolnost 0–10 a servis 200 / 500 / 1 000 / 2 000 / 4 000 h. Délku podpory (generované varianty 5 / 10 / 15 / 20 let) určuje jednotlivý kontrakt, nikoliv hráč v designeru. Odolnost zvyšuje konstrukční hmotnost, cenu, pracnost a odpor; životnost používá výslednou spolehlivost včetně kvality firmy. Od 23. 10. 2026 mají návrhy jednotný základ 30 000 h při 80% spolehlivosti, intervalu 1 000 h a odolnosti 0, nezávislý na tržním zařazení. Násobitele spolehlivosti, servisu a odolnosti se nemění; požadavky kontraktů, nehodové reference a zmrazené specifikace dodané flotily se nepřepisují. Samotné plynutí herního roku základ nezvyšuje.
- Civilní i státní dodávky registrují skutečně předané dávky se zmrazenými parametry. Registr sleduje hodiny, servis, uzemnění, technické/provozní nehody a postupné vyřazování mezi 100 a 120 % smluvního náletu. Stav náhody se ukládá.
- Továrny vyrábějí servisní sady a náhradní motory a opravují vrácené motory ve společném MD rozpočtu s novými letadly. Kapacitu omezuje velikost budovy a personál, automatizace snižuje pracnost. Existují rozšíření budovy, priority a zásoby i rozpracovanost podpory.
- Oba kontraktní kanály kontrolují životnost, servisní interval, délku podpory a provozní rozpočet. Výchozí generování preferuje z 25 % kupní cenu, ze 75 % celoživotní náklady. Přijaté ceny a nabídky se nemění zpětně.
- Reputace 0–100 má cenový násobitel 0,85–1,15. Měsíční změny rozlišují splněný servis, nedodržení podpory a technické ztráty vzhledem k expozici. Servisní příjmy, výrobní a dílenské náklady vstupují do účetnictví jednou, před kontrolou bankrotu.
- Save payload má verzi 12. Staré hry nedostávají domyšlenou historickou flotilu ani zpětné garance; eviduje se počátek nového registru. Starší kapacita továren má migrační zachování průchodnosti.

## Kde je ovládání

- Designer typu: odolnost, hodinový interval, očekávaná životnost a referenční provozní kalkulace. Podpora je pouze vysvětlena jako smluvní závazek, není zde volba její délky.
- My machines → Operating Fleet: souhrn podle typu/verze, aktivní a uzemněné kusy, ztráty, zásoby a datum podpory. Najetí na datum ukáže graf, kliknutí jej připne; detail lze otevřít i kliknutím bez hoveru. Garance a odhad aktivní flotily mají odlišné křivky.
- Továrny: stavební a personální MD kapacita, priority podpory/výroby, rozšíření a kapacitní výhled.
- Company: reputace a poslední příčiny změn. Ukončení podpory je oddělené od konce výroby; předčasné porušení zobrazuje dotčené kusy, kompenzaci a reputační dopad před potvrzením.

## Ověření a omezení

1. Povinný příkaz `Scripts\WrightTycoonWorkflow.cmd -Action Test` dne 19. 10. prošel: nejprve **401/401**, po změnách fáze 2 **407/407, 0 failed**. Výsledky `Logs/Workflow/editmode-20261019-000835.xml` a příslušný `.log`. První pokus odhalil chybějící explicitní referenci `Newtonsoft.Json.dll` v Game.Tests; opraveno. Předchozí licenční blokace z 18. 10. už neplatí.
2. Náhradní .NET kontrola zkompilovala všechny runtime zdroje modulů proti skutečným Unity knihovnám bez chyb. Oddělený reflexní běh čistých NUnit testů skončil **308 passed, 0 failed**; zahrnuje také skutečný JSON save payload a starší výchozí hodnoty. Není náhradou Unity test runneru, assembly discovery ani spuštění hry. Dočasné projekty jsou v ignorovaném `Logs/Workflow/FullCompileCheck`.
3. **1 215 dlouhodobých scénářů, 0 selhání invariantů**: různé dekády, kategorie, intervaly, odolnosti a seedy; zachování počtů, determinismus save/load, vyřazení do 120 % smluvní životnosti, zastavení příjmů po odchodu a obnovení provozu po nedostatku zásob. Výsledky: `Logs/Workflow/FleetBalanceCheck/results.txt`. Scénáře používají umělou servisní cenu a nedokazují vyváženou ziskovost skutečných továren.
4. Statistická regrese odhalila a opravila chybu losování nehod u jednočlenných skupin; samostatný regresní test nyní kontroluje i technické příčiny ztrát.
5. Audit zachování původní ziskovosti: 180 scénářů stejné flotily, 20 desetiletých výrobních scénářů a kontrola obou cenových služeb. Běh `editmode-20261019-090554.xml`: **410/410 funkčních testů**, ale ekonomický cíl **NEVYHOVUJE**. Cenová služba dosud nepřesouvá servisní příspěvek z prodejní marže a tarif nadhodnocuje opakovanou potřebu nových motorů. Nové/migrované kapacity mají odlišný dopad na průchodnost. Starý sandboxový běh s umělým tarifem není dokladem této ekonomiky; nový audit používá aktuální Unity zdroje. Ceníky během auditu nezměněny.

### Zbývá před vydáním

- Vizuálně ověřit designer, oba kontraktní kanály, flotilu, grafy a továrny na menším rozlišení a při stovkách kontraktů. Dosud bez skutečného spuštění GUI.
- Herně doladit dlouhodobá rizika a ekonomiku: v simulaci startu 1946 byl u jednoho vojenského scénáře úbytek 200/200 kusů nehodami před vyřazením. Agregované celoživotní ztráty roku 1946 dosahují přibližně 76 % sportovní, 83 % pístové dopravní a 94 % vojenské flotily. Jde o mnoho let expozice, nikoliv roční pravděpodobnost. Současné koeficienty jsou výchozí herní odhady, zejména vojenské profily a převod nehod na odpisy; nepovažovat je za doloženou historickou přesnost ani finální balanc.
- Ověřit využití MD, fixní režii a růst reputace v celé kampani. Dílčí reálné výrobní scénáře servisních marží nyní procházejí, ale nejsou úplnou kalibrací kampaně.

## Fáze 2 — první iterace (19. 10. 2026)

- Uživatel potvrdil rozsah: ekonomika, nehodovost a UI. Kontrola směru oddělila problém servisní ceny od vysokých kumulovaných nehodových ztrát.
- Nová nabídka konkrétního stroje používá sdílené receptury `FleetServiceBalance`: na událost sadu (2 % materiálu draku a 2,5 % jeho MD), čtvrtinu ceny nové kompletní sady motorů a její generálky (20 % materiálu, 35 % MD). Tarif je alespoň původních 2,5 % kompletní výrobní ceny a zároveň alespoň tato referenční výrobní cena / (1 − 0,15 dílenská práce − 0,20 cílový příspěvek).
- Jde o konzervativní financování první výměnné zásoby; její opakované použití a automatizace mohou pozdější příspěvek zvýšit. Cílových 20 % není čistá ani garantovaná marže: fixní tovární a kapacitní náklady a časování zásob zůstávají důležité. Další kalibrace kampaně je otevřená.
- Stejný tarif používá civilní kalkulace, státní kandidát, kontrola nabídky, zmrazená státní smlouva a skutečná dodávka. Historické kohorty a již zmrazené státní nabídky se nepřeceňují. Starší státní metriky bez nového tarifu zachovávají původní procentní výpočet; explicitně nulový tarif zůstává nulový. Civilní obchod nemá fázi přijetí nabídky, podmínky se zamykají při skutečné dodávce.
- Nové testy ověřují skutečnou výrobní spotřebu čtyř sad, prvních motorů a generálky pro 1/2/4 motory a různou pracnost, shodu civilního/státního tarifu, zachování starších cen a JSON snímku.
- Nehodové sazby nezměněny. Přidán otestovaný přepočet intenzity na referenční roční riziko při plánovaném náletu. UI jasně označuje konstantní aktuální sazbu/opotřebení, bez odstavení a vyřazování; nejde o predikci přežití. Dlouhodobých 1 215 scénářů bylo znovu spuštěno, 0 porušení invariantů.
- UI doplněno o tarif a referenční příspěvek v designeru, příjmy a peněžní náklady celé servisní sítě a rozlišení technických/provozních ztrát. Náklady zahrnují zásoby placené nyní; příspěvek není účetní zisk typu. Kapacitní výhled nově zahrnuje také generálky.
- Windows Computer Use se nepodařilo inicializovat: `failed to write kernel assets ... os error 3`. Proto nebylo možné ověřit skutečné obrazovky; testy ani kompilace nejsou vydávány za vizuální QA. Fáze 2 jako celek zůstává rozpracovaná.

## Zkušební launchery 0.6.7 — 19. 10. 2026

Na výslovný požadavek uživatele aktualizovány oba launchery přes `Scripts\WrightTycoonWorkflow.cmd -Action Checkpoint -Platform All`, bez vynechání testů či Windows smoke testu:

- Windows: `Builds/Windows/Release/WrightTycoon.exe`; macOS: `Builds/Mac/Release/WrightTycoon.app`. Oba manifesty uvádějí 0.6.7, ověřeno zabalení 14 historických událostí a odpovídajících obrázků.
- Poslední workflow: `Logs/Workflow/editmode-20261019-091701.xml` (**410/410**), `build-checkpoint-all-20261019-091701.log` (**obě sestavení uspěla**) a `windows-smoke-20261019-091701.log` (**načtení kampaně a připravený LaunchRunner**). Nejde o vizuální QA; macOS aplikace nebyla na Windows spuštěna.
- Windows IL2CPP pokus selhal kvůli chybějícím Visual Studio C++ build tools a Windows SDK. Zkušební checkpoint je proto sestaven s Mono pro obě platformy. Původní projektové nastavení IL2CPP bylo následně obnoveno; žádné systémové nástroje se neinstalovaly. Další IL2CPP build vyžaduje doplnění příslušného toolchainu.
- Přechod backendu vyžadoval nový výstupní adresář. Dosavadní Windows výstup zachován v `Builds/Windows/Release-before-mono-20261019-0916`. `Player Info` zkopírováno k novému launcheru a soubor v `Saves` ověřen shodným hashem. Původní záloha se nemaže.
- Checkpoint odstranil zastaralý `Builds/Mac/WrightTycoon-macOS-Release.tar.gz`; aktuální je `.app`. Nový distribuční archiv lze vytvořit čistým Release workflow podle [TestingAndLauncherWorkflow](TestingAndLauncherWorkflow.md).

Jde o aktuální implementaci k praktickému testování, nikoliv ekonomicky vyvážené vydání. Audit ziskovosti výše stále nevyhovuje a fáze 2 zůstává rozpracovaná.

## Oprava: podpora patří kontraktu, lifecycle údaje nesmí být skryté

Po uživatelském ověření 19. 10. odstraněna volba garantované podpory z designeru i jeho editovatelný stav. Nové civilní i státní nabídky používají `MinimumSupportMonths` kontraktu pro nákladovou kalkulaci a skutečně sjednaný servis. Samotný typ letadla už není zamítán kvůli historickému poli délky podpory. Při dodání se účinná smluvní nabídka zamkne i do specifikace kohorty; odlišné zakázky stejného typu mohou mít odlišné termíny.

Starší typové/payload údaje zůstávají pouze pro čitelnost savů. Dříve přijaté explicitní nabídky a starší rekonstrukce garance z uložených metrik se nepřepisují novým minimem. Regrese pokrývá nové smluvní 120 měsíců oproti starým 180 měsícům, včetně snímku bez explicitně uložené nabídky; nabídky 60/240 měsíců a jejich expirace se také ověřují.

Sdílený `ContractLifecycleText` nyní zásobuje civilní karty i mimo diagnostické rozbalení, poslední vygenerovanou dávku, státní požadavky, poznámkový blok a aktivní státní dodávky. Ukazuje požadovanou životnost, lifecycle cost limit/reference, servisní interval, smluvní podporu a cenovou preferenci. Porovnání kandidáta a připraveného civilního dodacího plánu ukazuje očekávanou technickou životnost a zákaznické náklady, nikoliv výrobní zisk. U již uzavřených státních smluv se navíc vypisuje skutečně sjednaná zmrazená garance.

Kontrakty s `LifecycleModelVersion=0` jsou výslovně označené jako staré bez sjednané životnosti/lifecycle cost; čísla se zpětně nevymýšlejí. Nově generované kontrakty používají verzi 1 a číselné požadavky. Existující ceny ani garance se touto opravou zpětně nemění.

Finální Checkpoint All: `Logs/Workflow/editmode-20261019-094558.xml` **419/419**, `build-checkpoint-all-20261019-094558.log` **obě sestavení uspěla**, `windows-smoke-20261019-094558.log` **úspěch**. Aktuální Windows/macOS launchery zůstávají 0.6.7, Mono; projektové nastavení IL2CPP po checkpointu obnoveno. Vizuální kontrola skutečných obrazovek tím není nahrazena.

## Servisní parametry a velikosti továren — 19. 10. 2026

- Designer motoru i karta v My Machines zobrazují povinnou výměnu/repasi každé čtyři hlavní servisní události letadla a náklad dílny na jeden motor (materiál + standardní práce). Samostatný motor nemá vlastní nový nezávislý interval ani samostatně účtovaný zákaznický servis: reference 4 000 h předpokládá hlavní servis letadla po 1 000 h.
- Designer typu a uložená karta typu zobrazují hlavní servis v hodinách, tarif za celé letadlo, tarif přepočtený na hodinu a interval výměny motorů. Referenční nová nabídka nepřepisuje ceny již dodaných kohort.
- Detail uloženého motoru agreguje všechny navázané typy včetně vyřazených z nabídky. Zobrazuje letuschopná/odstavená letadla, instalované motory podle zmrazeného počtu motorů v kohortě, rozsah skutečně sjednaných intervalů a tarifů, nejpozdější a nejbližší konec garance. Hover nad datem nebo kliknutí otevře souhrnný graf; jednotkou grafu jsou letadla, nikoliv nesprávně přepočtené motory.
- Souhrny neodvozují flotilu od historického počtu prodejů. Živé stroje zahrnují odstavené, ztráty a vyřazení se nezapočítávají. Vypnutí podpory nevymaže dosud platné garance. Starší prodeje před spuštěním registru nejsou rekonstruovány.
- Smazání typu s živou flotilou je blokováno (použít decommission). Motor používaný uloženým typem nelze smazat, aby zůstala zachována identita pro servisní přehled. Dříve odstraněné vazby nelze bezpečně odhadovat z názvů motorů.
- Továrny mají samostatný přehled Small / Medium / Large, skutečné MD, údržbu budovy a ceny i časy rozšíření. Dostupná kapacita je oddělená od fyzického limitu a personálního stropu; automatizace je zobrazena jako rychlostní bonus, ne velikost závodu. UI upozorňuje na odstávku při rozšíření a společný MD rozpočet pro letadla, motory i servis. Migrovaná kapacitní výjimka je výslovně označená. Karty velikostí a popisky se zalamují i v užším panelu.
- Nový `MaintenancePresentation` má 26 testovacích případů: intervaly, ceny, vadné vstupy, smíšené počty motorů, odstavení, vyřazení, expirace, porušené garance a ordinal identitu bez násobení při duplicitním výběru typů. Úplná sada: **445/445**, `Logs/Workflow/editmode-20261019-104539.xml`.

Ekonomické sazby ani nehodovost se tímto UI doplněním nemění. Dříve popsané odchylky dlouhodobé ziskovosti nadále vyžadují samostatné vyvážení. Automatické testy nejsou vydávány za vizuální kontrolu všech obrazovek.

Čistý Windows release úspěšně dokončen přes `Scripts\WrightTycoonWorkflow.cmd -Action Release -Platform Windows`: testy **445/445**, sestavení `Logs/Workflow/build-release-windows-20261019-104539.log` a smoke `Logs/Workflow/windows-smoke-20261019-104539.log` uspěly. Manifest potvrzuje 0.6.7, vytvořeno 19. 10. 2026 10:46 CEST. Výstup `Builds/Windows/Release/WrightTycoon.exe` je Mono; původní projektové IL2CPP nastavení obnoveno, odstraněny pouze Unity automaticky přidané prázdné ikonové záznamy. macOS tato aktualizace nemění.

Před vyčištěním přesného Windows výstupu uložen a hashem ověřen celý `Player Info` do `Builds/Backups/PlayerInfo-before-067-maintenance-20261019-1050`. Po sestavení obnoveno a hashem ověřeno všech 21 trvalých profilových souborů, včetně uložených her, obrázků a logů; Cache/Temp zůstávají prázdné. Uživatel následně potvrdil mazání pouze starých buildů a dočasných dat, nikoliv hráčských dat. Staré Windows Developer buildové soubory, `Release-before-mono-20261019-0916` a oba ZIPy `WT 0.6.6` přesunuty do koše (obnovitelné). Kompletní profil ze starého Windows buildu předem zvlášť zálohován a ověřen v `Builds/Backups/PlayerInfo-from-old-il2cpp-20261019`. Hash aktivního `Saves/saves.json` zůstal shodný. `Library`, aktuální Windows/macOS launcher, diagnostické logy a zálohy hráčských dat zachovány.

## Nové nabídky: fyzické náhradní díly místo servisní služby

Servisní interval v letových hodinách zůstává spouštěčem spotřeby. Jeden náhradní díl je konkrétní skladová položka pro daný typ a vyžaduje **1 % materiálu a 1 % standardní práce kompletního letadla**, včetně podílu instalovaných motorů v nákladové referenci. Procento práce platí stejně v MD i MH; továrna nadále interně účtuje MD. Automatizace snižuje práci právě jednou. Nejde o výrobu nového náhradního motoru.

Výchozí ladicí křivka: `1 + ceil(10 × (1 − reliability))`, s tolerancí na desetinných hranicích. Spolehlivost 100/90/80/50/0 % znamená 1/2/3/6/11 celých dílů za cyklus a letadlo. Použije se účinná spolehlivost se započtením kvality firmy; množství a cena se zmrazí do nabídky dodané kohorty. Chybějící díly odstaví neobsloužené stroje, nelze provést zlomek údržby. V jednom měsíci lze překročit více servisních prahů, každý vyžaduje vlastní množství dílů.

Příjem vzniká pouze za skutečně spotřebované díly při dokončeném servisním cyklu, nikoliv automatickou měsíční/hodinovou fakturou. Výroba a rozpracovanost zatěžují tovární materiál/práci a inventář; nový model nemá samostatný workshop příplatek, neúčtuje další očekávané opravy za hodinu a neobsahuje rezervu na výměnu motorů. Odhad externího zákaznického prostoje zůstává v lifecycle cost, není příjmem výrobce. Prodejní cena dílu používá dosavadní cíl příspěvku 20 % před režií (`náklad / 0.8`); není to závěr o celkové vyváženosti hry.

UI designeru a My Machines ukazuje počet dílů/cyklus, jejich materiál, výrobní práci a náklad, jednotkovou prodejní cenu a cenu cyklu. Ve Factory → Aircraft + Parts lze vybrat typ, vidět zásobu a nastavit ukládanou cílovou rezervu. Měsíční požadavek je nadcházející potřeba plus rezerva, odečítá se sklad i rozpracovanost. Dobrovolná rezerva nepřebírá prioritu garantovaného servisu; případný převod priority přesouvá WIP bez zdvojení zaplacené práce. Kapacitní graf zobrazuje očekávanou servisní spotřebu, nikoliv mimořádné doplňování rezervy.

Kompatibilita: `ServiceOfferSnapshot.MaintenanceModelVersion=0` zachovává původní sady, tarify, servisní práci a výměny podle již přijatých starých závazků. Nové civilní požadavky a nové státní kandidatury používají verzi 1. Civilní snapshot bez nového pole se obnovuje jako verze 0, takže ani dříve podepsané dosud nedodané kontrakty nepřejdou na jiný tarif. Sklad `AircraftSpareParts` a druh výrobní zakázky 3 jsou oddělené od starých `ServiceKits` a motorů; žádná konverze existujících zásob ani mazání savů neprobíhá. Státní metriky, nabídky, kohorty, zásoby, WIP a cílové rezervy se ukládají s novými údaji. UI starší programy výslovně odlišuje.

Nový model **neprovádí automatické výměny ani repase motorů**. Jejich budoucí pravidla jsou předmětem následující společné diskuse, ne této implementace. Původní ekonomický audit výše popisuje starý servisní model a jeho číselné výsledky nelze vydávat za vyvážení nové ekonomiky dílů. Diagnostický dlouhodobý test je aktualizován na skutečnou výrobu a spotřebu dílů.

Ověření: úplná sada **482/482** při Windows checkpointu (`editmode-20261019-110800.xml`) a znovu po aktualizaci dlouhodobého testovacího adaptéru (`editmode-20261019-110905.xml`). Windows incremental build a smoke prošly (`build-checkpoint-windows-20261019-110800.log`, `windows-smoke-20261019-110800.log`). Aktuální launcher 0.6.7 je Mono, manifest 19. 10. 2026 11:08 CEST; projektové nastavení IL2CPP obnoveno. macOS zatím obsahuje předchozí verzi této aktualizace. Vizuální kontrola všech obrazovek nebyla provedena.

Uložené hry se nemažou ani nepřepisují starší zálohou. Aktuální uživatelský save má vlastní novější datum 10:56 (před checkpointem), proto se neobnovuje profil z dřívějšího buildu. Nejnovější `Saves` navíc zkopírovány a hashem ověřeny v `Builds/Backups/Saves-after-parts-update-20261019-1110`.

## Zjednodušení aircraft type a Operation Costs

Navazující úprava odstraňuje servisní detailní blok z přehledu typu. Expected Life a Operation Costs jsou standardní řádky pod Cruise Fuel Burn; náklady jsou součtem amortizace referenční kupní ceny, skutečného počtu náhradních dílů do očekávaného vyřazení a paliva při ceně aktuálního herního měsíce. Rozpad, referenční cenový předpoklad a provenance paliva jsou v tooltipu. Výběr intervalu je200/500/1000/2000/4000h, staré zmrazené8000h zůstávají čitelné. Historické měsíční ceny EIA, odhad raného období a mezery jsou zdokumentované v [HistoricalFuelPrices](HistoricalFuelPrices.md); nejde o přepis již sjednaných lifecycle kalkulací. Finální sada516/516 a Windows checkpoint/smoke20261019-190157 uspěly; save se nezměnil. Aktuální0.6.7 Windows launcher obsahuje tuto úpravu, macOS nikoliv.
