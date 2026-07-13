☀️ # bUnit Test Implementation

**51 tests** covering **6 Blazor components** — all passing. This document describes the test infrastructure, per-component coverage, and next steps.

---

## Test Infrastructure

All bUnit tests live in `BlazorWebAppMovies.Tests/Bunit/` and share a common base class.

### `BunitTestBase.cs` — Shared Test Context

Base class that pre-configures a `TestContext` with all services Blazor components need:

| Service | How It's Provided |
|---------|------------------|
| **`IHttpClientFactory`** | Moq mock returning `HttpClient` backed by `MockHttpMessageHandler` |
| **`IJSRuntime`** | Moq mock — returns a dummy `IJSObjectReference` (required by QuickGrid's `OnAfterRenderAsync`) |
| **`NavigationManager`** | bUnit's built-in `FakeNavigationManager` (registering it as a singleton before rendering supports `[SupplyParameterFromQuery]`) |
| **`UserManager<User>`** | Minimal real `UserManager` instance with mocked store (required because `Home.razor` injects it unconditionally) |
| **Auth services** | `AddAuthorization()` + `AddCascadingAuthenticationState()` |
| **`AuthenticationStateProvider`** | `TestAuthStateProvider` set per-test via `SetAuthState()` |

### Key Helpers

| Method | Purpose |
|--------|---------|
| `SetAuthState(isAuthenticated, role?, userName?)` | Configures auth state *before* rendering (must be called before service provider is locked) |
| `RespondJson<T>(method, path, data, statusCode?)` | Configures a JSON API response for a given HTTP method + path |
| `RespondEmpty(method, path, statusCode?)` | Configures an empty API response |
| `NavigateTo(uri)` | Navigates `FakeNavigationManager` — sets query params for `[SupplyParameterFromQuery]` |
| `AssertContains(text, renderedComponent)` | Asserts rendered markup contains text (case-insensitive) |
| `AssertNotContains(text, renderedComponent)` | Asserts rendered markup does NOT contain text |
| `AssertElementExists(cssSelector, renderedComponent)` | Asserts an element matching CSS selector exists |
| `AssertElementNotExists(cssSelector, renderedComponent)` | Asserts no element matches the CSS selector |

### `MockHttpMessageHandler`

Custom `DelegatingHandler` that maps HTTP method + path combinations to pre-configured `HttpResponseMessage` responses. Returns 404 with a descriptive JSON body for unmatched requests — prevents silent test failures.

---

## Component Test Coverage

### 1. `NavMenuTests.cs` — 5 tests

| Test | What It Verifies |
|------|-----------------|
| `NavMenu_ShowsHomeWeatherMoviesAndClassicUiLinks_Always` | Home, Weather, Movies, Classic UI links always render |
| `NavMenu_AdminSeesUsersLink` | "Users" nav link renders for Admin role |
| `NavMenu_RegularUserDoesNotSeeUsersLink` | "Users" hidden for regular User role |
| `NavMenu_UnauthenticatedDoesNotSeeUsersLink` | "Users" hidden for anonymous users |
| `NavMenu_UsersLinkNavigatesToUserManagement` | Users link points to `usermanagement` |

### 2. `HomePageTests.cs` — 8 tests

| Test | What It Verifies |
|------|-----------------|
| `HomePage_Unauthenticated_ShowsSignInForm` | Email/password fields + Sign In button render |
| `HomePage_Unauthenticated_HidesWelcomeMessage` | Welcome/logged-in text absent |
| `HomePage_AuthenticatedUser_ShowsWelcomeMessage` | "Welcome" + username displayed |
| `HomePage_AuthenticatedUser_HidesSignInForm` | Sign In form absent when authenticated |
| `HomePage_AdminUser_SeesAdminPrivilegesMessage` | Admin alert + "Go to User Management" link |
| `HomePage_RegularUser_DoesNotSeeAdminPrivilegesMessage` | Admin alert absent for regular user |
| `HomePage_RegularUser_SeesUserPrivilegesMessage` | User alert + "Go edit movies!" link |
| `HomePage_ShowsUiSwitcher_WhenAuthenticated` | UI switcher card with Blazor + Classic links |

### 3. `MovieIndexTests.cs` — 10 tests

| Test | What It Verifies |
|------|-----------------|
| `Index_RendersMoviesFromApi` | Movies render from API response (title, genre) |
| `Index_ShowsSearchInput` | Search input with placeholder renders |
| `Index_ShowsCreateNewButton_WhenAuthenticated` | "Create New" link visible for auth'd users |
| `Index_HidesCreateNewButton_WhenNotAuthenticated` | "Create New" hidden for anonymous |
| `Index_ShowsEditAndDeleteButtonsPerRow_WhenAuthenticated` | 3 edit + 3 delete links when auth'd |
| `Index_HidesEditAndDeleteButtons_WhenNotAuthenticated` | No edit/delete links for anonymous |
| `Index_ShowsDetailsButtonPerRow_RegardlessOfAuth` | 3 details links always present |
| `Index_ShowsEmptyTable_WhenNoMovies` | Table renders (empty state) |
| `Index_DisplaysPosterThumbnail_WhenPosterUrlIsPresent` | Poster `<img>` with correct src |
| `Index_ShowsPlaceholder_WhenPosterUrlIsMissing` | Placeholder div when no PosterUrl |

### 4. `MovieCreateTests.cs` — 7 tests

| Test | What It Verifies                               |
|------|------------------------------------------------|
| `Create_RendersFormWithAllFields` | Title, Release Date, Genre, Price, Rating fields |
| `Create_HasAllRatingOptions` | All 5 rating options (G, PG, PG-13, R, NC-17)  |
| `Create_HasSubmitButtonAndBackLink` | Submit "Create" button + "Back to List" link   |
| `Create_SubmitValidForm_NavigatesToMoviesOnSuccess` | Successful submit → navigate to `/movies`      |
| `Create_SubmitConflict_ShowsErrorMessage` | 409 Conflict → error message displayed         |
| `Create_SubmitServerError_ShowsErrorMessage` | 500 Internal Server Error → generic error      |
| `Create_ShowsErrorDiv_WhenErrorMessageIsSet` | Error div hidden initially, shown after submit failure |

### 5. `MovieEditTests.cs` — 7 tests

| Test | What It Verifies |
|------|-----------------|
| `Edit_ShowsLoadingInitially` | "Loading..." shown initially, then resolves with data |
| `Edit_RendersFormWithMovieData_WhenLoaded` | Form populated with movie title in input |
| `Edit_ShowsSaveButtonAndBackLink` | "Save" submit + "Back to List" link |
| `Edit_ShowsMovieNotFound_WhenNotFound` | 404 → "Movie not found." message |
| `Edit_SubmitValidForm_NavigatesToMoviesOnSuccess` | Successful submit → navigate to `/movies` |
| `Edit_SubmitConflict_ShowsErrorMessage` | 409 Conflict → error message displayed |
| `Edit_SubmitServerError_ShowsErrorMessage` | 500 Internal Server Error → generic error |

### 6. `MovieDetailsTests.cs` — 8 tests

| Test | What It Verifies |
|------|-----------------|
| `Details_ShowsLoadingThenResolves` | "Loading..." then resolves |
| `Details_RendersMovieDetails_WhenLoaded` | Title, Genre, Price, Rating, field labels |
| `Details_ShowsPosterImage_WhenPosterUrlIsPresent` | `<img>` with correct src |
| `Details_ShowsNoPosterAvailable_WhenPosterUrlIsMissing` | "No poster available" text |
| `Details_ShowsFetchPosterButton_WhenNoPoster` | "Fetch Poster" button when no poster |
| `Details_ShowsEditAndBackLinks` | Edit link + Back to List link |
| `Details_ShowsMovieNotFound_WhenMovieNotFound` | 404 → "Movie not found." |
| `Details_ShowsMovieNotFound_OnApiError` | 500 → "Movie not found." (catch-all) |

### 7. `MovieDeleteTests.cs` — 6 tests

| Test | What It Verifies |
|------|-----------------|
| `Delete_ShowsLoadingThenResolves` | "Loading..." then resolves |
| `Delete_RendersMovieDetails_WhenLoaded` | Title, Genre, Price, Rating, confirmation message |
| `Delete_ShowsDeleteButtonAndBackLink` | "Delete" submit + "Back to List" link |
| `Delete_SubmitDelete_NavigatesToMovies` | Successful delete → navigate to `/movies` |
| `Delete_ShowsMovieNotFound_WhenNotFound` | 404 → "Movie not found." |
| `Delete_ShowsMovieNotFound_OnApiError` | 500 → "Movie not found." (catch-all) |

---

## Key Design Decisions

### FakeNavigationManager over Moq Mock
Components like `Edit.razor`, `Details.razor`, and `Delete.razor` use `[SupplyParameterFromQuery]` to receive the `Id` parameter. bUnit's `FakeNavigationManager` populates these from the URI, so `NavigateTo("/movies/edit?id=1")` before rendering correctly supplies the `Id` parameter.

### QuickGrid JS Interop
`QuickGrid` calls `IJSRuntime.InvokeAsync<IJSObjectReference>` in its `OnAfterRenderAsync` lifecycle method. Without a configured return value, the mock returns `null`, causing an `ArgumentNullException`. The base class sets this up automatically.

### UserManager Registration
`Home.razor` injects `UserManager<User>` with `@inject`. Even on the unauthenticated path where it's not used, the DI container must have it registered. The base class registers a minimal real instance (with mocked store) so `Home.razor` renders without error. Authenticated tests can override it with a mock that returns a specific user from `GetUserAsync`.

### Service Registration Order
All service registrations (including `SetAuthState`) must happen *before* the first `RenderComponent` call, because bUnit locks the service provider on first use.

---

## Next Steps

### 1. `UserManagement.razor` bUnit Tests
This component is more complex — it uses `UserManager` directly (not `IHttpClientFactory`) for all CRUD and modal state management. Recommended test coverage:
- User list rendering (name, email, roles)
- Create user form visibility toggle
- Edit user form pre-population
- Change password form visibility
- Delete confirmation modal
- Self-protection: admin cannot delete own account (delete button hidden)
- Success/error message display

### 2. `Lightbox.razor` Component Tests
- Lightbox opens/closes correctly
- Escape key closes lightbox
- Click on backdrop closes lightbox
- Child content renders inside lightbox

### 3. WebApplicationFactory Tests (Classic UI)
Layer 1 from the plan — tests the Razor Pages + API pipeline:
- Classic movies page renders
- Classic users page loads with JSON data
- Antiforgery validation on POST requests
- Auth cookie flow end-to-end

### 4. Playwright E2E Tests
Layer 3 from the plan — full browser-level tests:
- Login → Create movie → Verify → Delete → Logout
- UI switcher between Blazor ↔ Classic
- Admin user management flow
- JWT cookie auth flow
- SignalR circuit lifecycle

---

## Running the Tests

```bash
# All bUnit tests
dotnet test --filter "FullyQualifiedName~Bunit"

# Single test class
dotnet test --filter "FullyQualifiedName~MovieIndexTests"

# Single test
dotnet test --filter "FullyQualifiedName~Index_RendersMoviesFromApi"
```
