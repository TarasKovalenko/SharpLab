import languages from 'helpers/languages';
import getBranchesAsync from './server/get-branches-async';

import state from './state';
import uiAsync from './ui';

function applyUpdateResult(updateResult) {
    const result = {
        success: true,
        decompiled: updateResult.x.decompiled,
        errors: [],
        warnings: []
    };
    for (let diagnostic of updateResult.diagnostics) {
        if (diagnostic.severity === 'error') {
            result.success = false;
            result.errors.push(diagnostic);
        }
        else if (diagnostic.severity === 'warning') {
            result.warnings.push(diagnostic);
        }
    }
    this.result = result;
    this.loading = false;
}

function applyServerError(message) {
    this.result = {
        success: false,
        errors: [{ message: message }],
        warnings: []
    };
    this.loading = false;
}

function applyConnectionChange(connectionState) {
    this.online = (connectionState === 'open');
}

function getServiceUrl(branch) {
    if (!branch)
        return "ws://" + window.location.host + "/mirrorsharp";
    return branch.url.replace(/http/, 'ws') + '/mirrorsharp';
}

async function createAppAsync() {
    const data = Object.assign({
        languages: languages,

        branchGroups: [],
        branch: null,

        online: true,
        loading: true,

        result: {
            success: true,
            decompiled: '',
            errors: [],
            warnings: []
        }
    });
    state.load(data);

    const branchesPromise = (async () => {
        const branches = await getBranchesAsync();
        const groups = {};
        for (let branch of branches) {
            let group = groups[branch.group];
            if (!group) {
                group = { name: branch.group, branches: [] };
                groups[branch.group] = group;
                data.branchGroups.push(group);
            }
            group.branches.push(branch);
        }
        return branches;
    })();

    if (data.options.branchId) {
        const branches = await branchesPromise;
        data.branch = branches.filter(b => b.id === data.options.branchId)[0];
    }
    data.serviceUrl = getServiceUrl(data.branch);

    return {
        data,
        computed: {
            serverOptions: function() {
                return {
                    language: this.options.language,
                    optimize: this.options.release ? 'release' : 'debug',
                    'x-target-language': this.options.target
                };
            },
            status: function() {
                if (!this.online)
                    return { name: 'offline', color: '#aaa' };
                if (!this.result.success)
                    return { name: 'error', color: '#dc3912' };
                return { name: 'default', color: '#4684ee' };
            }
        },
        methods: { applyUpdateResult, applyServerError, applyConnectionChange }
    };
}

(async function runAsync() {
    const app = await createAppAsync();
    const ui = await uiAsync(app);
    const data = app.data;

    ui.watch('options', () => state.save(data), { deep: true });
    ui.watch('code', () => state.save(data));
    ui.watch('branch', value => {
        data.options.branchId = value ? value.id : null;
        data.loading = true;
        data.serviceUrl = getServiceUrl(value);
    });

    /*for (let name of ['options', 'branch']) {
        ui.watch(name,  () => app.processChangeAsync(), { deep: true });
    }*/
})();