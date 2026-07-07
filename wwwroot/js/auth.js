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
      // Cookie set by server with HttpOnly + Secure — just reload
      console.log("[auth] Login successful, cookie set by server, reloading");
      window.location.href = "/";
      return { success: true };
    } catch (error) {
      console.error("[auth] Error during login:", error);
      return { success: false, message: "Connection error: " + error.message };
    }
  },
  logout: async function () {
    console.log("[auth] Logging out");
    try {
      await fetch("/api/auth/logout", { method: "POST" });
    } catch (e) {
      console.warn("[auth] Logout request failed, continuing anyway", e);
    }
    window.location.href = "/";
  },
};
