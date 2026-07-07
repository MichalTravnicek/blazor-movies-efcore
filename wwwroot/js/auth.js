window.authService = {
  login: async function (email, password) {
    console.log("[auth] Attempting login for:", email);
    try {
      const response = await fetch("/api/auth/login", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ email, password }),
      });
      console.log("[auth] Response status:", response.status);
      if (!response.ok) {
        const errorData = await response.json();
        console.log("[auth] Login failed:", errorData);
        return {
          success: false,
          message: errorData.message || "Invalid credentials",
        };
      }
      const data = await response.json();
      console.log("[auth] Token received, setting cookie");
      document.cookie =
        "auth_token=" + data.token + "; path=/; max-age=86400; samesite=strict";
      console.log("[auth] Cookie set, reloading");
      window.location.href = "/";
      return { success: true };
    } catch (error) {
      console.error("[auth] Error during login:", error);
      return { success: false, message: "Connection error: " + error.message };
    }
  },
  logout: function () {
    console.log("[auth] Logging out, clearing cookie");
    document.cookie = "auth_token=; path=/; max-age=0; samesite=strict";
    window.location.href = "/";
  },
};
