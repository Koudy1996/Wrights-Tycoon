# Historická nehodovost pro model flotily

Výzkum k návrhu z 18. 10. 2026. Níže jsou oddělené doložené údaje a pracovní herní koeficienty. Nejde o bezpečnostní předpověď reálného letadla.

## Doložené zdroje a jednotky

1. [BTS, archiv tabulky U.S. General Aviation Safety Data](https://www.bts.gov/archive/publications/national_transportation_statistics/2005/table_02_14), řádky Total accidents a Total accidents, fatal. Hodnoty na 100 000 nalétaných hodin. Americké civilní všeobecné letectví, nikoliv univerzální světová flotila. Roky 1960, 1965 a 1970 zahrnují air taxi; data nejsou dokonale souměřitelná přes změnu definice. Následující tabulka uvádí jednotlivé referenční roky, NIKOLIV vypočtené desetileté průměry.

| Referenční rok | Všechny nehody / 100 000 h | Fatální nehody / 100 000 h |
| --- | ---: | ---: |
| 1960 | 36.53 | 3.27 |
| 1965 | 31.05 | 3.22 |
| 1970 | 18.10 | 2.46 |
| 1975 | 13.87 | 2.20 |
| 1980 | 9.86 | 1.70 |
| 1985 | 9.67 | 1.76 |
| 1990 | 7.86 | 1.56 |
| 2000 | 6.60 | 1.24 |

2. [NTSB, výsledky za 2020](https://www.ntsb.gov/news/press-releases/Pages/NR20211117.aspx): fatální nehody GA 1.049 / 100 000 h (2019: 1.064). [FAA Safety Briefing, březen/duben 2026](https://www.faa.gov/sites/faa.gov/files/MarApr2026.pdf) uvádí celkovou četnost GA za 2023 3.86 a fatální 0.65 / 100 000 h; 2022 4.30 a 0.68. Není to přímá míra odepsání draků.

3. [Boeing, Statistical Summary 1959–2024, vydání duben 2025](https://www.boeing.com/content/dam/boeing/v2/company/statsum.pdf), str. 8: desetileté bloky 1975–1984, 1985–1994, 1995–2004, 2005–2014 a 2015–2024, jednotka nehody / milion odletů. Poslední dva bloky vykazují pokles všech nehod 40 %, ztrát letadla 55 % a fatálních nehod 65 %. Str. 9: v letech 2015–2024 celkem 298 nehod, z toho 90 ztrát letadla; není proto správné odepsat stroj při každé nehodě. Str. 10 upřesňuje rozsah komerčních proudových letadel nad 60 000 lb a vyřazuje vojenský provoz a některé výrobní regiony. Grafové křivky bez tabulky nebyly vydávány za přesně odečtená desetiletá čísla.

4. [Airbus, A Statistical Analysis of Commercial Aviation Accidents 1958–2024](https://accidentstats.airbus.com/wp-content/uploads/2025/02/20241325_A-Statistical-analysis-of-commercial-aviation-accidents-2025-links.pdf), str. 4–5 definuje rozsah a hull loss jako zničení či škodu neekonomickou k opravě. Str. 17 ukazuje roční četnosti na milion letů a str. 21 desetiletý klouzavý průměr. Tato studie se týká velkých dopravních jetů; nevztahuje se přímo na malé pístové stroje, vojenské operace ani všechny business jety.

Tyto údaje podporují dlouhodobý pokles nehodovosti a nutnost různých provozních profilů. Nepodporují konkrétní násobitele za herní spolehlivost, slider nebo překročení životnosti; ty jsou návrhem balancování.

## Výchozí tabulka pro balancování po dekádách

Následující tabulka je návrh HERNÍ křivky pro celé kalendářní dekády. Není přepisem historických desetiletých průměrů. Hodnoty GA jsou zaokrouhlené kotvy přibližující výše uvedené referenční roky; dopravní proudová řada je prozatím odhad trendu, nikoliv přesný odečet Boeing grafu. Před zapnutím produkční simulace nahradit dostupné odhady agregací původních počtů/expozic; pokud expozice chybí, ponechat označení odhadu. Nelze přejmenovat blok Boeing 1975–1984 na „1970s“.

| Dekáda | GA všechny nehody / 100 000 h (návrh) | Dopravní jety hull loss / milion letů (prozatímní návrh) | Kvalita podkladu |
| --- | ---: | ---: | --- |
| 1940–1949 | 55 | nepoužít | Extrapolace, chybí srovnatelná ověřená řada; ve hře jen 1946–1949 |
| 1950–1959 | 45 | nepoužít před nástupem jetů | Extrapolace z raných civilních kotev; žádný falešný globální průměr |
| 1960–1969 | 34 | 8.0 | GA kotvy 1960/1965; jet pouze pracovní odhad |
| 1970–1979 | 16 | 3.0 | GA kotvy 1970/1975; jet pouze pracovní odhad |
| 1980–1989 | 10 | 1.8 | GA kotvy 1980/1985; jet pouze pracovní odhad |
| 1990–1999 | 8 | 1.2 | GA orientace na 1990; jet pouze pracovní odhad |
| 2000–2009 | 7 | 0.75 | GA orientace na 2000; jet pouze pracovní odhad |
| 2010–2019 | 5.5 | 0.35 | Interpolace; přesná agregace je součást datového úkolu |
| 2020–2029 | 4 | 0.30 | Neúplná dekáda, GA kotva 2023; jet pouze pracovní odhad |

Před 1960 používat samostatný transport-piston profil: pracovní převod GA křivky s vlastním provozním násobitelem, označit jako odhad. Business, turboprop, speciální práce a vojenský výcvik mají explicitní mapování profilů, nikoliv automatické použití dopravních jetů. Pro chybějící vojenská data zavést transparentní herní násobitel provozního profilu, nepředstírat historickou přesnost. Od roku 2030 držet poslední kalibrovanou hodnotu, bez předstírané historické prognózy.

GA převod nehoda -> odepsaný stroj potřebuje vlastní kalibraci závažnosti. Pracovní podíl 0.25 je návrh pro testovací scénáře, ne zjištěný historický GA podíl a ne převod fatálních nehod. V dopravní řadě s přímým hull loss jej již znovu nepoužívat. Bohužel nelze z poměru 90/298 velkých jetů dovodit totožný podíl pro GA.

## Výpočet, validace a omezení

### Ověření implementovaných kotev (18. 10. 2026)

Katalog `FleetAccidentCatalog` používá znovu ověřenou původní tabulku BTS výše, nikoli prostý průměr publikovaných ročních sazeb. Ověřené dvojice (všechny nehody; tisíce hodin): 1960 (4 793; 13 121), 1965 (5 196; 16 733), 1970 (4 712; 26 030), 1975 (3 995; 28 799), 1980 (3 590; 36 402), 1985 (2 739; 28 322), 1990 (2 242; 28 510), 2000 (1 837; 27 838). Poznámka a uvádí pro roky 1985/1990/2000 11/4/7 událostí zahrnutých do celkových počtů, ale nezahrnutých do sazeb; implementace je v čitateli odečítá. Výpočet je `počet × 100 / tisíce hodin`. Původní tištěné sazby a takto rekonstruovaný výpočet se mohou mírně lišit; žádný výsledek není označen jako přesný dekádový průměr. Kompletní srovnatelná roční expozice pro všechny dekády v tomto archivním výběru chybí.

Mezi těmito řídkými kotvami se interpoluje logaritmus intenzity. Roky 1946/1950 a 2010 zůstávají herními odhady, rok 2023 používá výše citovanou kotvu FAA. Celá výsledná interpolovaná řada má proto příznak odhadu. Přímá jetová hull-loss křivka zůstává transparentně označeným odhadem trendu; grafy Boeing nebyly přejmenovány na kalendářní dekády. Podíl technické příčiny 25 %, podíl neopravitelných GA nehod 25 % a násobitele specializovaných profilů jsou herní kalibrace.

Simulace při chybějícím servisu stroj odstaví přesně na servisním prahu. Dluh tak nemůže vytvářet další fiktivní letové hodiny; žádný další násobitel opotřebení za odstavení se nepřidává. Běžný servis nesnižuje kumulované opotřebení. Každá čtvrtá servisní událost zahrnuje výměnu motorů a vrácení vyjmutých motorů do fronty generálek. První model považuje vyjmuté motory při plánovaném servisu za opravitelné; motory z havarovaných ztracených strojů se nevracejí. Přímá dílenská práce stojí 15 % servisního tarifu, nad již oddělenými výrobními náklady dílů a motorů. To jsou výchozí herní ekonomické parametry.

- Při dostupných datech agregovat dekádu jako součet událostí / součet expozic × měřítko. Prostý průměr ročních četností dává jiný výsledek. Neúplné dekády a chybějící roky označit.
- Když zdroj udává četnost na lety, přepočet vyžaduje průměr délky letu konkrétního provozního profilu. Použití jedné konstanty pro všechna letadla by zkreslilo krátké a dlouhé tratě.
- Pro herní čas vyhladit přechod mezi dekádovými kotvami v logaritmu intenzity, aby se 1. ledna náhle nezměnilo riziko. Reference technologie/spolehlivosti normalizovat; nezlevňovat totéž riziko nezávisle dvakrát historickou érou a technologií.
- Základna už zahrnuje typickou údržbu a lidské/provozní vlivy; oddělení technické složky je herní model s dokumentovaným podílem. Nesmí vynulovat provozní riziko při reliability=1.
- Historická fatálnost není životnostní opotřebení. Exponenciální přepočet četnosti na pravděpodobnost má být rozměrově správný; losování omezené počtem aktivních kusů.
- Pro několik velikostí flotily a seedů vyhodnotit průměr, rozptyl a percentily; oddělit běžné odchody z důvodu stáří. Při referenčním stroji musí simulace reprodukovat zvolenou herní intenzitu s předem stanovenou tolerancí. Výsledek neoznačovat za ověření reálné letecké bezpečnosti.

Největší zbývající datové omezení jsou srovnatelné četnosti odepsání pro 1946–1959, vojenské/specializované operace a GA závažnost po dekádách. Implementace má fungovat i s označenou odhadovou tabulkou a pozdějším doplněním dat; kalibrace je výslovný krok plánu, nikoliv skrytý hotový výsledek.
