import viewModelBase = require("viewmodels/viewModelBase");
import replicationsSetup = require("models/database/replication/replicationsSetup");
import replicationConfig = require("models/database/replication/replicationConfig")
import replicationDestination = require("models/database/replication/replicationDestination");
import getDatabaseStatsCommand = require("commands/resources/getDatabaseStatsCommand");
import getReplicationsCommand = require("commands/database/replication/getReplicationsCommand");
import saveReplicationDocumentCommand = require("commands/database/replication/saveReplicationDocumentCommand");
import saveAutomaticConflictResolutionDocumentCommand = require("commands/database/replication/saveAutomaticConflictResolutionDocumentCommand");
import aceEditorBindingHandler = require("common/bindingHelpers/aceEditorBindingHandler");
import appUrl = require("common/appUrl");
import database = require("models/resources/database");
import resolveAllConflictsCommand = require("commands/database/replication/resolveAllConflictsCommand");
import eventsCollector = require("common/eventsCollector");

class replications extends viewModelBase {
    /* TODO
    replicationEnabled = ko.observable<boolean>(true);

    prefixForHilo = ko.observable<string>("");
    replicationConfig = ko.observable<replicationConfig>(new replicationConfig({ DocumentConflictResolution: "None" }));
    replicationsSetup = ko.observable<replicationsSetup>(new replicationsSetup({ Source: null, Destinations: [], ClientConfiguration: null, DocumentConflictResolution: null, Id: null }));

    replicationConfigDirtyFlag = new ko.DirtyFlag([]);
    replicationsSetupDirtyFlag = new ko.DirtyFlag([]);

    isConfigSaveEnabled: KnockoutComputed<boolean>;
    isSetupSaveEnabled: KnockoutComputed<boolean>;

    showRequestTimeoutRow: KnockoutComputed<boolean>;

    constructor() {
        super();
        aceEditorBindingHandler.install();

        this.showRequestTimeoutRow = ko.computed(() => this.replicationsSetup().showRequestTimeSlaThreshold());
    }

    canActivate(args: any): JQueryPromise<any> {
        var deferred = $.Deferred();
        var db = this.activeDatabase();
        if (db) {
            //TODO: we don't have active bundles in v4.0, let assume it is enabled
            this.replicationEnabled(true);
            $.when(this.fetchAutomaticConflictResolution(db), this.fetchReplications(db))
                .done(() => deferred.resolve({ can: true }))
                .fail(() => deferred.resolve({ redirect: appUrl.forSettings(db) }));

        }
        return deferred;
    }

    activate(args: any) {
        super.activate(args);
        this.updateHelpLink("7K1KES");

        this.replicationConfigDirtyFlag = new ko.DirtyFlag([this.replicationConfig]);
        this.isConfigSaveEnabled = ko.computed(() => this.replicationConfigDirtyFlag().isDirty());

        var replicationSetupDirtyFlagItems = [this.replicationsSetup, this.replicationsSetup().destinations(), this.replicationConfig, this.replicationsSetup().clientFailoverBehaviour];

        this.replicationsSetupDirtyFlag = new ko.DirtyFlag(replicationSetupDirtyFlagItems);

        this.isSetupSaveEnabled = ko.computed(() => this.replicationsSetupDirtyFlag().isDirty());

        var combinedFlag = ko.computed(() => {
            var rc = this.replicationConfigDirtyFlag().isDirty();
            var rs = this.replicationsSetupDirtyFlag().isDirty();
            return rc || rs || sp;
        });
        this.dirtyFlag = new ko.DirtyFlag([combinedFlag]);
    }

    attached() {
        super.attached();
        $.each(this.replicationsSetup().destinations(), this.addScriptHelpPopover);
    }

    fetchAutomaticConflictResolution(db: database): JQueryPromise<any> {
        var deferred = $.Deferred();
        new getEffectiveConflictResolutionCommand(db)
            .execute()
            .done((repConfig: configurationDocumentDto<replicationConfigDto>) => {
                this.replicationConfig(new replicationConfig(repConfig.MergedDocument));
            })
            .always(() => deferred.resolve({ can: true }));
        return deferred;
    }

    fetchReplications(db: database): JQueryPromise<any> {
        var deferred = $.Deferred();

        new getReplicationsCommand(db)
            .execute()
            .done((repSetup: Raven.Client.Documents.Replication.ReplicationDocument<Raven.Client.Documents.Replication.ReplicationDestination>) => {
                if (repSetup) {
                    this.replicationsSetup(new replicationsSetup(repSetup));    
                }
            })
            .always(() => deferred.resolve({ can: true }));
        return deferred;
    }

    addScriptHelpPopover() {
        //TODO: use popoverUtils.longWithHover
        $(".scriptPopover").popover({
            html: true,
            trigger: 'hover',
            container: ".form-horizontal",
            content:
            '<p>Return <code>null</code> in transform script to skip document from replication. </p>' +
            '<p>Example: </p>' +
            '<pre><span class="token keyword">if</span> (<span class="token keyword">this</span>.Region !== <span class="token string">"Europe"</span>) { <br />   <span class="token keyword">return null</span>; <br />}<br/><span class="token keyword">this</span>.Currency = <span class="token string">"EUR"</span>; </pre>'
        });
    }

    public onTransformCollectionClick(destination: replicationDestination, collectionName: string) {
        var collections = destination.specifiedCollections();
        var item = collections.find(c => c.collection() === collectionName);

        if (typeof (item.script()) === "undefined") {
            item.script("");
        } else {
            item.script(undefined);
        }

        destination.specifiedCollections.notifySubscribers();
    }

    createNewDestination() {
        eventsCollector.default.reportEvent("replications", "create");

        var db = this.activeDatabase();
        this.replicationsSetup().destinations.unshift(replicationDestination.empty(db.name));
        this.addScriptHelpPopover();
    }

    removeDestination(repl: replicationDestination) {
        eventsCollector.default.reportEvent("replications", "toggle-skip-for-all");

        this.replicationsSetup().destinations.remove(repl);
    }

    saveChanges() {
        eventsCollector.default.reportEvent("replications", "save");

        if (this.isConfigSaveEnabled())
            this.saveAutomaticConflictResolutionSettings();
        if (this.isSetupSaveEnabled()) {
            if (this.replicationsSetup().source()) {
                this.saveReplicationSetup();
            } else {
                var db = this.activeDatabase();
                if (db) {
                    new getDatabaseStatsCommand(db)
                        .execute()
                        .done(result=> {
                            this.prepareAndSaveReplicationSetup(result.DatabaseId);
                        });
                }
            }
        }
    }

    private prepareAndSaveReplicationSetup(source: string) {
        this.replicationsSetup().source(source);
        this.saveReplicationSetup();
    }

    private saveReplicationSetup() {
        var db = this.activeDatabase();
        if (db) {
            new saveReplicationDocumentCommand(this.replicationsSetup().toDto(), db)
                .execute()
                .done(() => {
                    this.replicationsSetupDirtyFlag().reset();
                    this.dirtyFlag().reset();
                });
        }
    }

    sendResolveAllConflictsCommand() {
        eventsCollector.default.reportEvent("replications", "resolve-all");

        var db = this.activeDatabase();
        if (db) {
            new resolveAllConflictsCommand(db).execute();
        } else {
            alert("No database selected! This error should not be seen."); //precaution to ease debugging - in case something bad happens
        }
    }


    saveAutomaticConflictResolutionSettings() {
        eventsCollector.default.reportEvent("replications", "save-auto-conflict-resolution");

        var db = this.activeDatabase();
        if (db) {
            new saveAutomaticConflictResolutionDocumentCommand(this.replicationConfig().toDto(), db)
                .execute()
                .done(() => {
                    this.replicationConfigDirtyFlag().reset();
                    this.dirtyFlag().reset();
                });
        }
    }

   */

}

export = replications; 
