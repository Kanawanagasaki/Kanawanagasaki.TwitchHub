
function getHeight(el) {
    if (el) return el.offsetHeight;
    return 0;
}

function getWidth(el) {
    if (el) return el.offsetWidth;
    return 0;
}

function getScrollWidth(el) {
    if (el) return el.scrollWidth;
    return 0;
}

function prettierFormat(lang, code) {
    try {
        return prettier.format(code, {
            parser: lang,
            plugins: prettierPlugins,
            tabWidth: 1
        });
    }
    catch (ex) {
        return null;
    }
}

function createLoopObj(instance)
{
    if(instance)
    {
        return {
            instance: instance,
            isRunnig: false,
            start: function()
            {
                this.isRunnig = true;
                this.loop();
            },
            loop: async function()
            {
                await this.instance.invokeMethodAsync("onTick");
                if(this.isRunnig)
                {
                    requestAnimationFrame(() => this.loop());
                }
            },
            stop: function()
            {
                this.isRunnig = false;
            }
        };
    }
}
