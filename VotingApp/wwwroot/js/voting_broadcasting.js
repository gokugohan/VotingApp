"use strict";

var connectionId;

var connection = new signalR
    .HubConnectionBuilder()
    .withUrl("/voting-data-broadcast")
    .withAutomaticReconnect()
    .build();



async function start() {
    try {
        await connection.start().then(() => console.log("Connection Started"));
    } catch (err) {
        toastr.error(err);
        $.notify({
            icon: "tim-icons icon-bell-55",
            message: "Upsss. Disabled!"
        });
        setTimeout(() => start(), 5000);
    }
}


connection.onclose(async () => {
    await start();

// Send the antiforgery token with every AJAX post (header name configured in ServiceExtension).
$.ajaxSetup({
    beforeSend: function (xhr) {
        var token = $('input[name="__RequestVerificationToken"]').first().val();
        if (token) {
            xhr.setRequestHeader("RequestVerificationToken", token);
        }
    }
});

function notifySaveFailure(xhr) {
    var message = 'Failed to save the vote';
    if (xhr && xhr.status === 403) message = 'You do not have permission to enter votes';
    else if (xhr && xhr.responseJSON && xhr.responseJSON.message) message = xhr.responseJSON.message;
    $.notify({ icon: "tim-icons icon-bell-55", message: message });
}
});

start();

$("body").on("submit", "#form_save_vote", function (ev) {
    ev.preventDefault();
    var data = $(this).serialize();

    $("#Total").val("");
    $.ajax({
        type: 'POST',
        url: '/Votings/SaveVote',
        data: data,
        success: function (result) {
            $.notify({
                icon: "tim-icons icon-bell-55",
                message: result.message || (result.success ? 'Saved' : 'Failed to save the vote')
            });

        },
        error: notifySaveFailure
    })
});


$("body").on("click", ".btn-submit-total-vote", function (ev) {
    let id = $(this).data("party-id");
    let htmlELementId = $(this).data("input-element-id");
    let pollingStationAreaId = $("#PollingStationAreaId option:selected").val();
    let total = $("#" + htmlELementId).val();

    let data = {};
    data.CandidateId = id;
    data.PollingStationAreaId = pollingStationAreaId;
    data.Total = total;

    $.ajax({
        type: 'POST',
        url: '/Votings/SaveVote',
        data: data,
        success: function (result) {
            $.notify({
                icon: "tim-icons icon-bell-55",
                message: result.message || (result.success ? 'Saved' : 'Failed to save the vote')
            });

        },
        error: notifySaveFailure
    })
});

connection.on("OnbroadcastVotingDataListener", function (message) {
    $.notify({
        icon: "tim-icons icon-bell-55",
        message: message
    });
});
