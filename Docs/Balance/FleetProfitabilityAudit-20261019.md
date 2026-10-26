# Audit ziskovosti prodejů a údržby — 19. 10. 2026

**Výsledek: NEVYHOVUJE požadavku přibližně zachovat původní ziskovost.** Funkční testy prošly; ekonomická neutralita nikoliv. Ceníky ani kapacitní koeficienty v tomto diagnostickém kroku nebyly změněny.

Uživatel požaduje zachovat přibližně původní celkový výdělek, ale rozdělit jej mezi prodej a následnou údržbu. Pro audit bylo zvoleno orientační pásmo ±10 % původního příspěvku; nejde o schválený finální herní koeficient.

## Reprodukce a co přesně se porovnává

- Referenční původní kód: git HEAD `7e27abe4cdd8b6f134b98e107e35ee494bc0de46`, před novou údržbou. Pravidla původních marží, kusových limitů, zaokrouhlování zaměstnanců a přenosu MD byla porovnána se zdroji pomocí `git show HEAD:...`.
- Nový zdrojový test: `Assets/WrightTycoon/Tests/EditMode/Aircraft/FleetProfitabilityAuditTests.cs`. Používá aktuální továrnu, skutečné servisní zásoby/generálky, registr flotily, náhodné nehody, měsíční priority a účetnictví. Neodkazuje na starou předkompilovanou sandbox DLL.
- Příkaz: `Scripts\WrightTycoonWorkflow.cmd -Action Test`. **410/410 funkčních testů prošlo**, včetně tří nových diagnostických auditů. Ty kontrolují soulad peněz, kusů a MD; jejich úspěch NETVRDÍ, že ekonomika je vyvážená. XML: `Logs/Workflow/editmode-20261019-090554.xml`.
- Kontrolovaný typ má materiál draku $150 000 a motorů dohromady $70 000, pracnost 100 + 20 MD a dva motory. Základní kompletní cena je $229 600. Prodejní cena je normalizována na původní malou zakázku: $301 350, tedy 31,25% přirážka k referenční výrobní ceně. Typ není plně fyzikálně optimalizovaný návrh ani reprezentativní celá kampaň.
- Stejná reputace a technologie, žádné tržní výkyvy, výzkum, daně či úvěrování. U desetileté tovární simulace je poptávka neomezená, všechny dokončené kusy se ihned prodávají bez časování skutečných zakázek a financování neomezuje výrobu. Stavba/instalace je v obou větvích dokončená před měřeným oknem; původní továrna je rekonstruovaný model, nikoliv běžící stará herní binárka.
- Kompletní číselné výsledky: [FleetProfitabilityAudit-20261019.json](FleetProfitabilityAudit-20261019.json).

## 1. Stejných 20 dodaných letadel

**180 scénářů:** smluvní nálet 4 000 / 10 000 / 20 000 h × pět servisních intervalů × ruční/nejvyšší automatizace × ukončení/pokračování po desetileté garanci × tři seedy. Provoz 100 h/měsíc, pístový profil od roku 2000, spolehlivost 0,88, technický limit 30 000 h. Model používá skutečné nehody a odchody mezi 100–120 % smluvního náletu. Po ukončení podpory odstavené stroje nevytvářejí fiktivní příjmy.

Porovnává se příspěvek po materiálu, výrobní práci, servisní dílenské práci a aktivitě servisního vybavení. Společná údržba budovy a její počáteční kapitál zde nejsou alokované jednotlivé dávce; nejde o čistý zisk celé společnosti. Tyto náklady jsou zahrnuty samostatně v kalendářní části níže.

| Ruční výroba, pokračující podpora; průměr tří seedů | Původní příspěvek | Nový příspěvek | Změna |
|---|---:|---:|---:|
| 4 000 h, servis po 8 000 h — žádná událost | $1,432 mil. | $1,434 mil. | +0,12 % |
| 10 000 h, servis po 1 000 h | $1,432 mil. | $5,038 mil. | **+252 %** |
| 20 000 h, servis po 500 h | $1,432 mil. | $18,634 mil. | **+1 201 %** |

Pouze **12 ze 180** případů splnilo orientační ±10% pásmo; všechny tyto případy mají nulový počet servisních událostí. **168 případů nevyhovělo.** U nejvýraznějšího jednotlivého scénáře dosahuje nový příspěvek přibližně 13,55násobku původního. Nejde o tvrzení o průměrné ziskovosti skutečné kampaně.

## 2. Stejná továrna a deset let provozu

**20 scénářů:** pět automatizací × nová malá/migrovaná kapacita × bez údržby/s údržbou. Nová letadla a servis skutečně soutěží o MD. Započteny materiály, zaokrouhlené mzdy, všechny zaplacené servisní zásoby a práce, provoz vybavení a $600 000 měsíční údržby budovy. Počáteční kapitál budovy a vybavení se eviduje také, ale tabulka ukazuje provozní cash flow PŘED tímto shodným počátečním výdajem. Jeho odečtení není další provozní náklad.

| Nová malá budova, údržba zapnutá | Původní provozní cash flow | Nové provozní cash flow | Změna | Dodáno dříve → nyní |
|---|---:|---:|---:|---:|
| Ruční výroba | $14,00 mil. | $176,40 mil. | +1 160 % | 1 200 → 1 200 |
| Přípravky | $145,94 mil. | $357,22 mil. | +145 % | 3 000 → 1 985 |
| Mechanizované buňky | $370,68 mil. | $426,59 mil. | +15 % | 6 000 → 2 291 |
| Automatická linka | $603,01 mil. | $529,32 mil. | −12 % | 9 000 → 2 747 |
| Nejvyšší automatizace | $844,69 mil. | $717,71 mil. | −15 % | 12 000 → 3 574 |

U migrované kapacity nejvyšší automatizace vychází naopak $2 187,48 mil. oproti původním $844,69 mil. Nové a migrované budovy proto nelze kalibrovat jedním společným výsledkem. V případech bez servisu migrovaný model reprodukuje starou provozní ekonomiku přibližně do 0,6 %; menší rozdíly odpovídají novému zaokrouhlování mezd. To poskytuje kontrolu rekonstruované reference.

Peníze po počátečním kapitálu jsou ve strojových datech odděleně (`cashAfterCapital`). Například ruční původní varianta má v desetiletém okně po pořízení budovy stále záporný kumulovaný cash flow; procentní růst provozního cash flow proto nelze vydávat za návratnost kapitálu.

## 3. Příčiny rozdílu

1. **Prodejní cena se zatím cíleně nesnižuje o budoucí servisní příspěvek.** Skutečná civilní i státní cenová služba ve scénáři s neomezujícím celoživotním rozpočtem vrací stejnou cenu pro lifecycle 0 a 1: civil $4 660 124,47, stát $2 707 606,34. Servisní zisk se tedy přičítá.
2. **Rezerva na motory je opakovaná.** `FleetServiceBalance` do každé čtvrté události promítá nové motory plus generálku; skutečná výroba však opravené motory znovu používá. Tarif $38 061,54 u kontrolovaného typu pak dlouhodobě vydělává více, než naznačuje cílových 20 % v prvním cyklu.
3. **Kapacita mění počet prodejů.** Fyzická budova omezuje nové automatizované linky jinak než starý kusový strop; servis navíc část MD spotřebuje. Snížit jednotně všechny ceny by některé nové továrny dále poškodilo.
4. **Státní bonus je navázán na prodejní cenu.** Při úplném splnění je 10 % hodnoty dodávky. Případný přesun očekávaného čistého příspěvku ze státní prodejní ceny musí počítat i změnu tohoto bonusu, nikoliv automaticky odečíst celou servisní tržbu. Vývojová záloha je oddělená a není servisním příjmem.

## Co je potřeba pro navazující vyvážení

- Odhadovat skutečné náklady servisního cyklu: díly, počáteční výměnnou zásobu, následné generálky, přežití flotily, časování servisu a příslušnou režii. Nový motor neúčtovat do očekávaných nákladů stále znovu, pokud se vrací opravitelný kus.
- Rozdělovat **původní očekávaný příspěvek**, nikoliv tržbu. Příklad pracovního cíle může být 70 % prodej / 30 % údržba; tento podíl zatím nebyl schválen ani zaveden. Nulová očekávaná údržba nesmí vést k neopodstatněné slevě z letadla.
- Pro ilustraci u ručního scénáře 10 000/1 000 h by 70/30 rozdělení podle již pozorovaných průměrných nákladů znamenalo prodej přibližně $279 868/kus místo $301 350 a servis přibližně $17 912/událost místo $38 062. Je to výpočet ZPĚTNĚ z testu, nikoliv nasaditelný ceník: budoucí výpočet musí používat očekávání bez znalosti vylosovaných nehod.
- Samostatně doladit velikosti, rozšíření a fixní režii nových továren v kalendářních scénářích. Zachovat konečnou fyzickou kapacitu a konkurenční využití MD; nevracet skrytou neomezenou motorovou výrobu.
- Cenové změny použít pouze pro nové nabídky. Existující zmrazené smlouvy a kohorty nepřeceňovat; nevyplácet fiktivní kompenzace při nehodě či nesplněném servisu jen kvůli udržení cílového zisku.

Předchozích 1 215 dlouhodobých scénářů používalo umělý tarif a předkompilovanou sandbox DLL. Dokládaly kontrolované invarianty tehdejší simulace, **nikoliv ziskovost aktuálního ceníku**. Tento audit proto vychází z aktuálních Unity zdrojů a skutečných výrobních nákladů.
