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
            if (result.success) {
                connection.invoke("BroadcastVotingData", result.message).then(function () { }).catch(function (err) { return console.log(err); })
                $.notify({
                    icon: "tim-icons icon-bell-55",
                    message: result.message
                });
            }

        },
        error: function () {
            $.notify({
                icon: "tim-icons icon-bell-55",
                message: 'Failed to receive the Data'
            });
            console.log('Failed ');
        }
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
            if (result.success) {
                connection.invoke("BroadcastVotingData", result.message).then(function () { }).catch(function (err) { return console.log(err); })
                $.notify({
                    icon: "tim-icons icon-bell-55",
                    message: result.message
                });
            }

        },
        error: function () {
            $.notify({
                icon: "tim-icons icon-bell-55",
                message: 'Failed to receive the Data'
            });
            console.log('Failed ');
        }
    })
});

connection.on("OnbroadcastVotingDataListener", function (message) {
    $.notify({
        icon: "tim-icons icon-bell-55",
        message: message
    });
});
