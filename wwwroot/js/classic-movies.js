/**
 * classic-movies.js — jQuery Datatables CRUD for the Classic UI Movies page
 *
 * Communicates with the existing /api/movies endpoints.
 * Auth cookie (auth_token) is sent automatically by the browser.
 */

(function () {
  "use strict";

  // Only run on the movies page
  if ($("#moviesTable").length === 0) return;

  console.log(
    "[classic] Movies page initialized, isAuth:",
    window.__classicAuth,
  );

  const API = "/api/movies";
  const isAuth = window.__classicAuth && window.__classicAuth.isAuthenticated;

  function showToast(title, message, type) {
    $("#toastTitle").text(title);
    $("#toastMessage").text(message);
    $("#toast")
      .removeClass("bg-danger bg-success")
      .addClass(
        type === "error" ? "bg-danger text-white" : "bg-success text-white",
      );
    var toast = new bootstrap.Toast($("#toast")[0]);
    toast.show();
  }

  function formatDate(dateStr) {
    if (!dateStr) return "";
    var d = new Date(dateStr + "T00:00:00");
    return d.toLocaleDateString("en-US", {
      year: "numeric",
      month: "short",
      day: "numeric",
    });
  }

  function toInputDate(dateStr) {
    if (!dateStr) return "";
    return dateStr.substring(0, 10);
  }

  var table = $("#moviesTable").DataTable({
    ajax: {
      url: API,
      type: "GET",
      dataSrc: "",
      error: function (jqXHR, textStatus, errorThrown) {
        console.error(
          "[classic] Movies AJAX error:",
          textStatus,
          errorThrown,
          jqXHR.responseText,
        );
      },
    },
    columns: [
      { data: "title" },
      {
        data: "releaseDate",
        render: function (data) {
          return formatDate(data);
        },
      },
      { data: "genre" },
      {
        data: "price",
        render: function (data) {
          return "$" + parseFloat(data).toFixed(2);
        },
      },
      { data: "rating" },
      {
        data: null,
        orderable: false,
        render: function (row) {
          var btn =
            '<button class="btn btn-sm btn-info me-1 details-btn" data-movie-id="' +
            row.id +
            '">Details</button>';
          if (isAuth) {
            btn +=
              '<button class="btn btn-sm btn-warning me-1 edit-btn" data-movie-id="' +
              row.id +
              '">Edit</button>' +
              '<button class="btn btn-sm btn-danger delete-btn" data-movie-id="' +
              row.id +
              '">Delete</button>';
          }
          return btn;
        },
      },
    ],
    order: [[1, "asc"]],
    pageLength: 10,
    language: {
      search: "Filter:",
      emptyTable: "No movies found.",
    },
  });

  function validateForm(formId) {
    var form = $(formId)[0];
    if (!form.checkValidity()) {
      form.classList.add("was-validated");
      return false;
    }
    return true;
  }

  // ─── Create ───
  $("#createSaveBtn").on("click", function () {
    if (!validateForm("#createForm")) return;
    $.ajax({
      url: API,
      method: "POST",
      contentType: "application/json",
      data: JSON.stringify({
        title: $("#createTitle").val().trim(),
        releaseDate: $("#createReleaseDate").val(),
        genre: $("#createGenre").val().trim(),
        price: parseFloat($("#createPrice").val()),
        rating: $("#createRating").val(),
      }),
      success: function () {
        $("#createModal").modal("hide");
        $("#createForm")[0].reset();
        $("#createForm").removeClass("was-validated");
        table.ajax.reload();
        showToast("Success", "Movie created.", "success");
      },
      error: function (xhr) {
        var msg = "Failed to create movie.";
        if (xhr.responseJSON && xhr.responseJSON.message)
          msg = xhr.responseJSON.message;
        showToast("Error", msg, "error");
      },
    });
  });

  function populateModal(id, callback) {
    $.get(API + "/" + id, callback).fail(function () {
      showToast("Error", "Movie not found.", "error");
    });
  }

  // ─── Edit ───
  $("#moviesTable").on("click", ".edit-btn", function () {
    populateModal($(this).data("movieId"), function (movie) {
      $("#editId").val(movie.id);
      $("#editTitle").val(movie.title);
      $("#editReleaseDate").val(toInputDate(movie.releaseDate));
      $("#editGenre").val(movie.genre);
      $("#editPrice").val(movie.price);
      $("#editRating").val(movie.rating);
      $("#editModal").modal("show");
    });
  });

  $("#editSaveBtn").on("click", function () {
    if (!validateForm("#editForm")) return;
    $.ajax({
      url: API + "/" + $("#editId").val(),
      method: "PUT",
      contentType: "application/json",
      data: JSON.stringify({
        title: $("#editTitle").val().trim(),
        releaseDate: $("#editReleaseDate").val(),
        genre: $("#editGenre").val().trim(),
        price: parseFloat($("#editPrice").val()),
        rating: $("#editRating").val(),
      }),
      success: function () {
        $("#editModal").modal("hide");
        table.ajax.reload();
        showToast("Success", "Movie updated.", "success");
      },
      error: function (xhr) {
        var msg = "Failed to update movie.";
        if (xhr.responseJSON && xhr.responseJSON.message)
          msg = xhr.responseJSON.message;
        showToast("Error", msg, "error");
      },
    });
  });

  // ─── Delete ───
  $("#moviesTable").on("click", ".delete-btn", function () {
    var id = $(this).data("movieId");
    populateModal(id, function (movie) {
      $("#deleteTitle").text(movie.title);
      $("#deleteConfirmBtn").data("id", id);
      $("#deleteModal").modal("show");
    });
  });

  $("#deleteConfirmBtn").on("click", function () {
    $.ajax({
      url: API + "/" + $(this).data("id"),
      method: "DELETE",
      success: function () {
        $("#deleteModal").modal("hide");
        table.ajax.reload();
        showToast("Success", "Movie deleted.", "success");
      },
      error: function (xhr) {
        var msg = "Failed to delete movie.";
        if (xhr.responseJSON && xhr.responseJSON.message)
          msg = xhr.responseJSON.message;
        showToast("Error", msg, "error");
      },
    });
  });

  // ─── Details ───
  $("#moviesTable").on("click", ".details-btn", function () {
    populateModal($(this).data("movieId"), function (movie) {
      $("#detailsTitleValue").text(movie.title);
      $("#detailsReleaseDate").text(formatDate(movie.releaseDate));
      $("#detailsGenre").text(movie.genre);
      $("#detailsPrice").text("$" + parseFloat(movie.price).toFixed(2));
      $("#detailsRating").text(movie.rating);
      $("#detailsModal").modal("show");
    });
  });

  // ─── Reset validation on modal close ───
  $(".modal").on("hidden.bs.modal", function () {
    $(this).find("form").removeClass("was-validated");
  });
})();
