/**
 * classic-users.js — jQuery Datatables for the Classic UI User Management page
 * Data is passed server-side via window.__usersData.
 */
(function () {
  "use strict";

  if ($("#usersTable").length === 0) return;

  var usersData = window.__usersData || [];

  function showUserToast(title, message, type) {
    $("#userToastTitle").text(title);
    $("#userToastMessage").text(message);
    $("#userToast")
      .removeClass("bg-danger bg-success")
      .addClass(
        type === "error" ? "bg-danger text-white" : "bg-success text-white",
      );
    var toast = new bootstrap.Toast($("#userToast")[0]);
    toast.show();
  }

  var usersTable = $("#usersTable").DataTable({
    data: usersData,
    columns: [
      { data: "name" },
      { data: "email" },
      {
        data: "roles",
        render: function (data) {
          if (!data || data.length === 0)
            return '<span class="badge bg-secondary">None</span>';
          return data
            .map(function (r) {
              var cls = r === "Admin" ? "bg-danger" : "bg-primary";
              return '<span class="badge ' + cls + ' me-1">' + r + "</span>";
            })
            .join("");
        },
      },
      {
        data: null,
        orderable: false,
        render: function (row) {
          var btns =
            '<button class="btn btn-sm btn-warning me-1 edit-user-btn" data-user-id="' +
            row.id +
            '">Edit</button>' +
            '<button class="btn btn-sm btn-outline-info me-1 change-password-btn" data-user-id="' +
            row.id +
            '">Password</button>';
          if (window.__canDeleteUsers && row.id !== window.__currentUserId) {
            btns +=
              '<button class="btn btn-sm btn-outline-danger delete-user-btn" data-user-id="' +
              row.id +
              '">Delete</button>';
          }
          return btns;
        },
      },
    ],
    order: [[0, "asc"]],
    pageLength: 10,
    language: {
      search: "Filter:",
      emptyTable: "No users found.",
    },
  });

  function saveForm(url, method, data, formId, modalId, successMsg) {
    $.ajax({
      url: url,
      method: method,
      contentType: "application/json",
      data: JSON.stringify(data),
      success: function () {
        $(modalId).modal("hide");
        $(formId)[0].reset();
        $(formId).removeClass("was-validated");
        showUserToast("Success", successMsg, "success");
        location.reload();
      },
      error: function (xhr) {
        var msg = "Request failed.";
        if (xhr.responseJSON && xhr.responseJSON.message)
          msg = xhr.responseJSON.message;
        showUserToast("Error", msg, "error");
      },
    });
  }

  function validateForm(formId) {
    var form = $(formId)[0];
    if (!form.checkValidity()) {
      form.classList.add("was-validated");
      return false;
    }
    return true;
  }

  // ─── Create User ───
  $("#createUserSaveBtn").on("click", function () {
    if (!validateForm("#createUserForm")) return;
    saveForm(
      "/api/admin/users",
      "POST",
      {
        name: $("#userName").val().trim(),
        email: $("#userEmail").val().trim(),
        password: $("#userPassword").val(),
        role: $("#userRole").val(),
      },
      "#createUserForm",
      "#createUserModal",
      "User created.",
    );
  });

  // ─── Edit User ───
  $("#usersTable").on("click", ".edit-user-btn", function () {
    var rowData = usersTable.row($(this).closest("tr")).data();
    if (!rowData) return;
    $("#editUserId").val(rowData.id);
    $("#editUserName").val(rowData.name);
    $("#editUserEmail").val(rowData.email);
    $("#editUserRole").val(rowData.isAdmin ? "Admin" : "User");
    $("#editUserModal").modal("show");
  });

  $("#editUserSaveBtn").on("click", function () {
    if (!validateForm("#editUserForm")) return;
    saveForm(
      "/api/admin/users/" + $("#editUserId").val(),
      "PUT",
      {
        name: $("#editUserName").val().trim(),
        email: $("#editUserEmail").val().trim(),
        role: $("#editUserRole").val(),
      },
      "#editUserForm",
      "#editUserModal",
      "User updated.",
    );
  });

  // ─── Change Password ───
  $("#usersTable").on("click", ".change-password-btn", function () {
    $("#changePasswordUserId").val($(this).data("userId"));
    $("#changePasswordForm")[0].reset();
    $("#changePasswordForm").removeClass("was-validated");
    $("#changePasswordModal").modal("show");
  });

  $("#changePasswordSaveBtn").on("click", function () {
    var newPw = $("#newPassword").val();
    if (newPw !== $("#confirmNewPassword").val()) {
      showUserToast("Error", "Passwords do not match.", "error");
      return;
    }
    if (!validateForm("#changePasswordForm")) return;

    $.ajax({
      url: "/api/admin/users/" + $("#changePasswordUserId").val() + "/password",
      method: "PUT",
      contentType: "application/json",
      data: JSON.stringify({ newPassword: newPw }),
      success: function () {
        $("#changePasswordModal").modal("hide");
        showUserToast("Success", "Password changed.", "success");
      },
      error: function (xhr) {
        var msg = "Failed to change password.";
        if (xhr.responseJSON && xhr.responseJSON.message)
          msg = xhr.responseJSON.message;
        showUserToast("Error", msg, "error");
      },
    });
  });

  // ─── Delete User ───
  $("#usersTable").on("click", ".delete-user-btn", function () {
    var rowData = usersTable.row($(this).closest("tr")).data();
    $("#deleteUserName").text(rowData ? rowData.name : "this user");
    $("#deleteUserConfirmBtn").data("id", $(this).data("userId"));
    $("#deleteUserModal").modal("show");
  });

  $("#deleteUserConfirmBtn").on("click", function () {
    $.ajax({
      url: "/api/admin/users/" + $(this).data("id"),
      method: "DELETE",
      success: function () {
        $("#deleteUserModal").modal("hide");
        showUserToast("Success", "User deleted.", "success");
        location.reload();
      },
      error: function (xhr) {
        var msg = "Failed to delete user.";
        if (xhr.responseJSON && xhr.responseJSON.message)
          msg = xhr.responseJSON.message;
        showUserToast("Error", msg, "error");
      },
    });
  });

  // Reset validation on modal close
  $(".modal").on("hidden.bs.modal", function () {
    $(this).find("form").removeClass("was-validated");
  });
})();
