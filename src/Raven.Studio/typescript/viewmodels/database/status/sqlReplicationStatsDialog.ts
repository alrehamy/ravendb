import database = require("models/resources/database");
import dialogViewModelBase = require("viewmodels/dialogViewModelBase");
import dialog = require("plugins/dialog");
import getSqlReplicationStatsCommand = require("commands/database/sqlReplication/getSqlReplicationStatsCommand");

class sqlReplicationStatsDialog extends dialogViewModelBase {
    repData = ko.observable<Raven.Server.Documents.ETL.EtlProcessStatistics>();

    constructor(private sqlReplicationName:string) {
        super();
    }

    activate(args: any) {
        this.fetchSqlReplicationStats();
    }

    cancel() {
        dialog.close(this);
    }

    fetchSqlReplicationStats(): JQueryPromise<Raven.Server.Documents.ETL.EtlProcessStatistics> {
        return new getSqlReplicationStatsCommand(this.activeDatabase(), this.sqlReplicationName)
            .execute()
            .done((result: Raven.Server.Documents.ETL.EtlProcessStatistics) => {
                this.repData(result);
            }).fail(() => this.repData(null));
    }
}

export = sqlReplicationStatsDialog;
