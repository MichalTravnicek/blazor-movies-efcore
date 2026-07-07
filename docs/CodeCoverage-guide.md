# Code Coverage — Pokyny a aktuální stav

> Projekt: `BlazorWebAppMovies` (.NET 9)
> Datum: 2026-07-07

---

## Jak spustit code coverage

### 1. Spuštění testů s coverage

```bash
dotnet test --collect:"XPlat Code Coverage"
```

Výstup: coverage soubor v `BlazorWebAppMovies.Tests/TestResults/<guid>/coverage.cobertura.xml`

### 2. Zobrazení výsledků

#### Textový report (přehledný)

```bash
# Nainstalovat nástroj (stačí jednou)
dotnet tool install -g dotnet-reportgenerator-globaltool

# Vygenerovat textový report
reportgenerator "-reports:BlazorWebAppMovies.Tests/TestResults/**/coverage.cobertura.xml" "-targetdir:temp/coverage-report" "-reporttypes:TextSummary"

# Zobrazit
cat temp/coverage-report/Summary.txt
```

#### HTML report (interaktivní)

```bash
reportgenerator "-reports:BlazorWebAppMovies.Tests/TestResults/**/coverage.cobertura.xml" "-targetdir:temp/coverage-report" "-reporttypes:Html"

# Otevřít v prohlížeči
start temp/coverage-report/index.html
```

#### HTML se souhrnem (kompaktní)

```bash
reportgenerator "-reports:BlazorWebAppMovies.Tests/TestResults/**/coverage.cobertura.xml" "-targetdir:temp/coverage-report" "-reporttypes:HtmlSummary"

# Otevřít
start temp/coverage-report/summary.html
```

---

## Aktuální stav (2026-07-07)

### Celkové statistiky

| Metrika | Hodnota |
|---|---|
| **Line coverage** | **11.7%** |
| **Branch coverage** | 21.2% |
| **Method coverage** | 39.2% |
| **Covered lines** | 283 z 2 416 |
| **Assemblies** | 1 |

> ⚠️ Celkové % je zkreslené — Blazor UI komponenty (Razor Pages) nejsou testovány jednotkovými testy a mají 0% coverage.

---

### Coverage podle vrstev

| Vrstva | Coverage | Stav |
|---|---|---|
| **Controllers** | | |
| `AuthController` | **100%** | ✅ |
| `MoviesController` | **94.7%** | ✅ |
| **Data** | | |
| `BlazorWebAppMoviesContext` | **100%** | ✅ |
| `DbContextProvider` | 64.7% | 🟡 |
| `SeedData` | **100%** | ✅ |
| `DesignTimeDbContextFactory` | **100%** | ✅ |
| `SqliteDbContextProvider` | 55.5% | 🟡 |
| `SqlServerDbContextProvider` | 55.5% | 🟡 |
| **Modely** | | |
| `Movie` | **100%** | ✅ |
| `User` | **100%** | ✅ |
| **DTO** | | |
| `MovieDto` | **100%** | ✅ |
| `CreateMovieDto` | **100%** | ✅ |
| `UpdateMovieDto` | **100%** | ✅ |
| **Mapping** | | |
| `MovieProfile` (AutoMapper) | **100%** | ✅ |
| **Swagger** | | |
| `SwaggerExampleFilter` | 0% | ❌ |
| `SwaggerLoginDescriptionFilter` | 0% | ❌ |
| **Blazor UI** | | |
| Všechny Pages (Create, Delete, Edit, Index...) | **0%** | ❌ |
| Layout, NavMenu, App | **0%** | ❌ |
| **Infrastructure** | | |
| `Program.cs` | 0% | ❌ |
| Migrations | 0% | ❌ |

---

### Co je dobře pokryto (94-100%)

| Třída | Tests |
|---|---|
| AuthController | 17 testů |
| MoviesController | 29 testů |
| Entity modely (Movie, User) | ~70 DB testů |
| DTO validace | 16 testů |
| AutoMapper profil | 11 testů |

### Co chybí

| Třída | Proč není pokryto | Jak testovat |
|---|---|---|
| **Blazor Pages** | UI testy — jednotkově netestovatelné | Playwright / bUnit / E2E |
| **Program.cs** | Startup konfigurace | `WebApplicationFactory` (integrační testy) |
| **Swagger filtry** | Kosmetika, nízká priorita | Jednotkový test s `SchemaFilterContext` |
| **DbContextProvider** | Logika výběru providera | Chybí testy pro SQL Server větev |
| **Migrations** | Generovaný kód | Není třeba testovat |

---

## Doporučení pro zvýšení coverage

### Krátkodobě (nízké úsilí)

| Co | Odhad | Zvýšení |
|---|---|---|
| Přidat testy pro `DbContextProvider` (SQL Server větev) | ~15 min | +~10% u Data vrstvy |
| Přidat integrační test pro `Program.cs` | ~30 min | 0% → ~60% u Program.cs |

### Dlouhodobě (vyšší úsilí)

| Co | Odhad | Zvýšení |
|---|---|---|
| bUnit testy pro Blazor komponenty | ~2-3h | 0% → ~50% u UI |
| E2E testy (Playwright) | ~4h | 0% → ~30% u UI |

---

## Rychlý příkaz pro opakované spuštění

```bash
# Jedním příkazem: testy + coverage + textový report
dotnet test --collect:"XPlat Code Coverage" && reportgenerator "-reports:BlazorWebAppMovies.Tests/TestResults/**/coverage.cobertura.xml" "-targetdir:temp/coverage-report" "-reporttypes:TextSummary" && type temp\coverage-report\Summary.txt
```